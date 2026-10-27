<script setup lang="ts">
import { getCurrentOrganization } from '@/api/generated/current-organization/current-organization'
import { accessProblemOf } from '@/lib/access-problem'
import { auditSentence } from '@/lib/audit-sentence'
import { formatDateTime } from '@/lib/date-format'
import { errorMessage } from '@/lib/error-message'
import { initialsOf } from '@/lib/initials'

interface EntryRow {
  id: string | number
  actor: string
  text: string
  initials: string
  time: string
  occurredAt: string
}

interface DayGroup {
  key: string
  heading: string
  rows: EntryRow[]
}

const client = getCurrentOrganization()
const { t, te, locale } = useI18n()
const auth = useAuthStore()

const { items, loading, loadingMore, hasMore, error, loadMore, reset } = useCursorList({
  fetchPage: (cursor) => client.currentOrganizationGetAuditLog({ cursor }),
})

onMounted(reset)

const accessProblem = computed(() => accessProblemOf(error.value))

// A failed first page leaves nothing loaded and no cursor; a failed "Show more" keeps its cursor.
const loadFailed = computed(
  () => error.value !== null && !accessProblem.value && !hasMore.value && items.value.length === 0,
)
const moreFailed = computed(() => error.value !== null && !accessProblem.value && !loadFailed.value)
const isEmpty = computed(() => items.value.length === 0 && !hasMore.value)

// formatDateTime gives "HH:mm, dd/MM/yyyy" in the browser's time zone: the date part is the local
// calendar day, which is what entries are grouped by.
function timeAndDate(iso: string) {
  const [time = '', date = ''] = formatDateTime(iso, locale.value).split(', ')
  return { time, date }
}

const days = computed<DayGroup[]>(() => {
  const now = new Date()
  const today = timeAndDate(now.toISOString()).date
  const yesterday = timeAndDate(
    new Date(now.getFullYear(), now.getMonth(), now.getDate() - 1, 12).toISOString(),
  ).date

  function headingOf(date: string) {
    if (date === today) return t('auditLog.today', { date })
    if (date === yesterday) return t('auditLog.yesterday', { date })
    return date
  }

  const groups = new Map<string, DayGroup>()
  for (const entry of items.value) {
    const { time, date } = timeAndDate(entry.occurredAt)
    const { actor, text } = auditSentence(entry, {
      t,
      te,
      organizationName: auth.organizationName ?? '',
    })
    const group = groups.get(date) ?? { key: date, heading: headingOf(date), rows: [] }
    group.rows.push({
      id: entry.id,
      actor,
      text,
      initials: entry.actorKind === 'SystemAdministrator' ? 'FH' : initialsOf(entry.actorName),
      time,
      occurredAt: entry.occurredAt,
    })
    groups.set(date, group)
  }
  return [...groups.values()]
})
</script>

<template>
  <AccessProblemState v-if="accessProblem" :kind="accessProblem" />

  <div v-else class="mx-auto flex w-full max-w-3xl flex-col gap-4 p-4 desktop:p-6">
    <h1 class="text-section text-ink">{{ t('auditLog.title') }}</h1>

    <div v-if="loading" class="flex flex-col" aria-busy="true" data-testid="audit-log-skeleton">
      <div v-for="i in 6" :key="i" class="flex items-start gap-3 border-b border-line py-3">
        <n-skeleton circle :width="36" :height="36" />
        <div class="flex min-w-0 flex-1 flex-col gap-2">
          <n-skeleton text style="width: 75%" />
          <n-skeleton text style="width: 25%" />
        </div>
      </div>
    </div>

    <InlineNotice v-else-if="loadFailed" tone="danger" data-testid="audit-log-error">
      {{ t('auditLog.loadError') }}
      <template #action>
        <n-button size="small" secondary data-testid="audit-log-retry" @click="reset">
          {{ t('auditLog.retry') }}
        </n-button>
      </template>
    </InlineNotice>

    <EmptyState v-else-if="isEmpty" :title="t('auditLog.empty')" data-testid="audit-log-empty" />

    <template v-else>
      <section v-for="day in days" :key="day.key" class="flex flex-col">
        <h2 class="mb-1 text-caption text-ink-muted" data-testid="audit-log-day">
          {{ day.heading }}
        </h2>
        <ul class="flex flex-col">
          <li
            v-for="row in day.rows"
            :key="row.id"
            class="flex items-start gap-3 border-b border-line py-3 last:border-b-0"
            :data-testid="`audit-log-row-${row.id}`"
          >
            <div
              class="flex size-9 shrink-0 items-center justify-center rounded-full bg-accent text-caption text-accent-contrast"
              aria-hidden="true"
              data-testid="audit-log-avatar"
            >
              {{ row.initials }}
            </div>
            <div
              class="flex min-w-0 flex-1 flex-col gap-0.5 desktop:flex-row desktop:items-baseline desktop:justify-between desktop:gap-4"
            >
              <p class="min-w-0 break-words text-row text-ink">
                <strong class="font-medium">{{ row.actor }}</strong> {{ row.text }}
              </p>
              <time
                :datetime="row.occurredAt"
                class="shrink-0 text-caption tabular-nums text-ink-muted"
              >
                {{ row.time }}
              </time>
            </div>
          </li>
        </ul>
      </section>

      <div v-if="hasMore" class="flex flex-col items-center gap-2 py-2">
        <InlineNotice v-if="moreFailed" tone="danger" data-testid="audit-log-more-error">
          {{ errorMessage(error, t, te) }}
        </InlineNotice>
        <n-button
          secondary
          :loading="loadingMore"
          data-testid="audit-log-show-more"
          @click="loadMore"
        >
          {{ t('common.showMore') }}
        </n-button>
      </div>
    </template>
  </div>
</template>
