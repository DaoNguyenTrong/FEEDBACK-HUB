import { requireEnv } from './fixtures'

interface MailpitSearchResult {
  messages: { ID: string }[]
}

interface MailpitMessage {
  Text: string
}

const ATTEMPTS = 10
const DELAY_MS = 500
const INVITE_LINK = /#\/invite\?token=[^\s"'<>]+/

async function getJson<T>(url: string): Promise<T> {
  const response = await fetch(url)
  if (!response.ok) throw new Error(`Mailpit answered ${response.status} for ${url}`)
  return (await response.json()) as T
}

/**
 * Returns the `#/invite?token=...` part of the link in the newest message to `email`. Only the fragment:
 * the link's origin is the API's `FrontendBaseUrl`, not the preview server, so `page.goto` adds `baseURL`.
 * The mail may land after the API answers, so it retries up to 10 times, 500 ms apart.
 */
export async function latestInvitationLink(email: string): Promise<string> {
  const base = requireEnv('E2E_MAILPIT_URL')
  const query = encodeURIComponent(`to:"${email}"`)

  for (let attempt = 1; attempt <= ATTEMPTS; attempt++) {
    const found = await getJson<MailpitSearchResult>(`${base}api/v1/search?query=${query}`)
    // Mailpit lists the newest message first.
    const newest = found.messages[0]
    if (newest) {
      const message = await getJson<MailpitMessage>(`${base}api/v1/message/${newest.ID}`)
      const link = INVITE_LINK.exec(message.Text)?.[0]
      if (link) return link
    }
    if (attempt < ATTEMPTS) await new Promise((resolve) => setTimeout(resolve, DELAY_MS))
  }

  throw new Error(`No invitation email for ${email} reached Mailpit after ${ATTEMPTS} attempts.`)
}
