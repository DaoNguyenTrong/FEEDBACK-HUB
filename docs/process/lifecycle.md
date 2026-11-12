# Vòng đời phát triển phần mềm

Tài liệu này mô tả đường đi từ ý tưởng đến phát hành, ai làm từng giai đoạn, ai duyệt và khi nào xong. `AGENTS.md` giữ các quy tắc chung (commit, push và PR, công cụ, nơi ghi quyết định); chi tiết từng giai đoạn nằm ở đây.

## 1. Các giai đoạn

| # | Giai đoạn | Sản phẩm đầu ra | Người làm | Cổng duyệt | Xong khi |
|---|---|---|---|---|---|
| 0 | Ý tưởng và tầm nhìn | Tầm nhìn, danh sách module, thứ tự ưu tiên | Phiên chính (`superpowers:brainstorming`) cùng người dùng | Người dùng | Danh mục module trong `docs/prd/README.md` được chốt |
| 1 | Yêu cầu | PRD trong `docs/prd/` | `product-manager`, review bởi `business-analyst` | Người dùng | PRD ở trạng thái *Approved* |
| 2 | Thiết kế giao diện | Bảng thiết kế trên canvas, gắn mã yêu cầu | `ui-designer` | Người dùng | Mọi FR có giao diện đều có bảng; người dùng duyệt |
| 3 | Thiết kế kỹ thuật | Tài liệu trong `docs/tech/` | `tech-lead` (`appsec` trước với module nhạy cảm) | Người dùng | Tài liệu *Approved*, mọi FR/AC có mục thiết kế |
| 4 | Kế hoạch | Kế hoạch trong `docs/superpowers/plans/` | Phiên chính (`superpowers:writing-plans`) | Người dùng | Người dùng duyệt kế hoạch; đồng thời quyết định có cấp quyền push và PR không |
| 5 | Triển khai | Code, test, mục `docs/feature-map.md` | `implementer`, mỗi lần một task | `code-reviewer` giữa các task | Mọi task của phase xong, `scripts/test-gate.sh` đạt |
| 6 | Review và merge | PR được merge vào `dev` | `pr-reviewer` (PR chỉ có tài liệu: `/git-accept`) | Tự động, trừ vùng cần người dùng | PR vào `dev`; sau PR cuối của module, phiên chính chạy retro (§8) |
| 7 | Phát hành | Bản phát hành, changelog | Skill `release-kit:git-release` | Người dùng | Tag và merge vào `main` |
| 8 | Vận hành và phản hồi | Feedback thật từ chính FeedbackHub | Người dùng | — | Quay lại giai đoạn 0, 1 hoặc 5 theo cỡ việc |

Phiên chính là người điều phối: gửi mọi câu hỏi cho `solution-architect` trả lời thay người dùng, chỉ hỏi người dùng những câu agent đó chuyển lên, chuyển câu trả lời cho agent, gọi agent theo thứ tự, và không tự làm thay công việc của agent. Subagent không hỏi được người dùng và không gọi được agent khác.

## 2. Cỡ công việc

Phiên chính chốt cỡ ở đầu mỗi việc và nói rõ cho người dùng; người dùng có thể đổi.

| Cỡ | Khi nào | Đường đi |
|---|---|---|
| **S** | Sửa lỗi, việc vặt, đổi nhỏ không thay đổi hành vi hay hợp đồng | Bỏ giai đoạn 0–4. Lỗi: test thất bại trước. `implementer` → `code-reviewer` → `pr-reviewer` |
| **M** | Đổi hoặc thêm hành vi trong module đã có | Sửa PRD (tăng phiên bản, về *In review* rồi duyệt lại), thiết kế và tài liệu kỹ thuật chỉ phần bị ảnh hưởng, kế hoạch ngắn, rồi giai đoạn 5–6 |
| **L** | Module mới, hoặc thay đổi chạm quyền, phiên, định danh | Đủ giai đoạn 0–6, `appsec` nếu nhạy cảm |

Cỡ S không được thêm hành vi người dùng nhìn thấy hay đổi API. Nếu việc S hóa ra cần đổi hành vi, dừng và nâng lên M.

## 3. Cổng giữa các giai đoạn

- **Không vào giai đoạn sau khi giai đoạn trước chưa duyệt.** Ngoại lệ đã ghi: module nhạy cảm (PRD02) có thể viết tài liệu kỹ thuật song song với thiết kế giao diện.
- **Câu hỏi nghiệp vụ phát sinh ở giai đoạn sau quay về PRD**, do `product-manager` sửa, không quyết ở nơi phát hiện. Câu hỏi kỹ thuật thuộc `tech-lead`.
- **Thay đổi sau khi đã duyệt:** sửa PRD đưa nó về *In review*, kéo theo xét lại thiết kế, tài liệu kỹ thuật và kế hoạch bị ảnh hưởng. Tài liệu bị ảnh hưởng cũng về *In review* và tăng phiên bản. Ngoại lệ: điền hay sửa dòng liên kết và metadata (*Thiết kế*, *Kế hoạch*, *Cập nhật*) không tính là thay đổi.
- **Sau 3 lượt sửa PR vẫn chưa đạt:** `pr-reviewer` trả `ESCALATE`, chỉ ra tài liệu cần nghĩ lại (PRD, thiết kế hay kế hoạch). Không có lượt sửa thứ 4.
- **Vùng cần người dùng:** xác thực, token, cookie, phiên, quyền, đổi tổ chức, khoá API, định danh widget, `.claude/`, `.github/`, `AGENTS.md`, cấu hình CI và Lefthook, EF migration, cấu hình triển khai lưu trữ/Redis/SignalR, và thay đổi `.claude/decisions.md` sửa hay xoá mục cũ hoặc có dòng `Supersedes:`. `pr-reviewer` không tự merge các PR này.

## 4. Lệch thiết kế khi triển khai

Khi code không thể làm đúng tài liệu kỹ thuật đã duyệt:

| Mức | Khi nào | Xử lý |
|---|---|---|
| **Nhỏ** | Không đổi hợp đồng API, quyền, mô hình dữ liệu lưu trữ hay hành vi người dùng thấy (ví dụ tên class nội bộ, thêm index, tách service) | `implementer` làm tiếp, ghi vào mục *Lệch nhỏ so với thiết kế* của báo cáo và mô tả PR kèm lý do. `pr-reviewer` chặn nếu chỗ lệch không thật sự nhỏ hoặc không được ghi. Tài liệu kỹ thuật không sửa |
| **Lớn** | Đổi bất kỳ điều nào ở trên | `implementer` dừng task, trả chỗ lệch kèm bằng chứng và đề xuất. Phiên chính gọi `tech-lead` sửa tài liệu trên nhánh đang làm (tăng phiên bản, về *In review*); câu hỏi nghiệp vụ đi qua `product-manager` trước. Người dùng duyệt lại, rồi `implementer` làm tiếp. Bản sửa đi chung PR với code |

Không chắc thì coi là lớn. Để lệch lớn lộ ra sớm, kế hoạch đặt phần có rủi ro kỹ thuật cao thành task spike đầu tiên.

## 5. Commit tài liệu

- **PRD và tài liệu kỹ thuật** nằm trên nhánh `chore/*` riêng cắt từ `dev` ngay từ bản nháp đầu: phiên chính commit mỗi phiên bản lên nhánh đó và mở một **PR nháp** chỉ chứa tài liệu, để lịch sử các phiên bản và báo cáo review (comment trên PR) không mất giữa các phiên làm việc. Bắt đầu soạn một tài liệu là lệnh push và mở PR nháp cho nó. PR nháp không được merge; khi người dùng duyệt (trạng thái *Approved*), phiên chính commit bản duyệt, chuyển PR sang sẵn sàng và merge qua `/git-accept`.
- **Kế hoạch** được phiên chính commit trên nhánh `feature/*` của nó, đi chung PR với code.
- **Mọi PR**, kể cả PR chỉ có tài liệu, có mục `## Changelog` theo định dạng của `release-kit:git-mr` (`- Added: …`, `- Fixed: …`, hoặc chỉ `none` cho thay đổi nội bộ), và luôn có một mục `## ` khác theo sau: `git-release` đọc mục này tới heading `## ` kế tiếp, nên dòng chữ tự do ở cuối (dòng ghi nguồn) sẽ làm nó hỏng. `git-release` dựng CHANGELOG của bản phát hành từ các mục này; không sửa `CHANGELOG.md` bằng tay.
- **Sửa tài liệu do lệch lớn** được phiên chính commit trên nhánh đang làm, đi chung PR với code; vì vậy `pr-reviewer` luôn đối chiếu code với đúng phiên bản tài liệu.
- `implementer` không sửa và không commit PRD, tài liệu kỹ thuật hay kế hoạch.

## 5b. Độ dài tài liệu

- **Kế hoạch** mô tả cho từng task: giao diện (chữ ký public, DTO, endpoint), danh sách tệp, ca kiểm thử và tiêu chí nghiệm thu. Chỉ chép đoạn mã ngắn khi chữ ký hoặc thuật toán khó tả bằng lời. Không viết sẵn mã hoàn chỉnh: `implementer` viết lại nó, và bản trong kế hoạch sẽ lệch khỏi mã thật.
- **PRD** của một module thường nhắm khoảng 500 dòng. Đây là giới hạn mềm: module lớn được vượt nếu ghi lý do ở đầu PRD. Không cắt phạm vi để giữ số dòng, vì cắt phạm vi là quyết định của người dùng.

## 6. Quyền push và mở PR

Mặc định chỉ push hay mở PR khi người dùng ra lệnh. Ở giai đoạn 4, khi duyệt kế hoạch, người dùng có thể cấp **một lần cho cả kế hoạch**: mỗi khi một phase xong và `scripts/test-gate.sh` đạt, `implementer` được push nhánh của phase đó và mở một PR vào `dev`. Quyền này ghi trong đầu kế hoạch và trong brief giao cho `implementer`; không áp dụng cho `main`, và không thay cho cổng `pr-reviewer`.

**Mỗi phase một PR.** Mỗi phase của kế hoạch là một nhánh `feature/<module>-phase-<n>` và một PR vào `dev`. Một PR nhắm tối đa khoảng 1.500 dòng mã nguồn, không tính test và tệp sinh tự động (`shared/openapi/`, `frontend/src/api/generated/`); phase chạm vùng cần người dùng (§3) nên nhỏ hơn. Khi lập kế hoạch, phase ước lượng vượt mức này thì tách làm hai. Nếu vượt khi đang làm thì tách tại ranh giới task, không tách giữa task. Con số 1.500 được xem lại ở retro cuối module 01.

**Xếp chồng một tầng.** Để triển khai không phải chờ review, nhánh phase N+1 được cắt từ nhánh phase N khi PR của phase N đã qua `pr-reviewer` (đạt, hoặc đang ở lượt sửa đầu tiên). Không xếp chồng quá một tầng, và không xếp chồng khi phase N đã qua hơn một lượt sửa. Khi phase N merge, nhánh N+1 rebase lên `origin/dev` rồi mới mở hoặc review PR, nên quy tắc một PR một lúc bên dưới vẫn giữ.

**Mỗi PR một mục đích.** Không gộp việc khác loại vào một PR, nhất là tài liệu đang chờ duyệt với thay đổi tooling hay cấu hình: PR gộp chỉ merge được khi phần chậm nhất xong, và phải đi qua cổng chặt nhất trong các phần của nó. Khi người dùng yêu cầu gộp, phiên chính nói rõ cái giá đó trước khi làm.

**Thứ tự merge — lịch sử gần thẳng.** Mỗi lúc chỉ một PR vào `dev` được review và merge. Trước khi review, nhánh phải được rebase lên `origin/dev` mới nhất (`git rebase origin/dev`, rồi `git push --force-with-lease`); merge bằng `gh pr merge --merge` để mỗi PR vẫn có một merge commit đánh dấu ranh giới. Merge xong PR này mới rebase nhánh kế tiếp. Nhánh chưa được rebase thì `pr-reviewer` và `/git-accept` trả *chưa sẵn sàng*, không tính là một lượt. Phiên chính điều phối thứ tự và yêu cầu rebase.

**Dọn nhánh sau khi merge.** Ngay sau khi merge, người merge (`pr-reviewer`, `/git-accept`, hoặc phiên chính khi người dùng cho merge) xoá nhánh trên remote, chỉ sau khi xác nhận nhánh đã nằm trong `origin/dev`: `git fetch --prune origin && git merge-base --is-ancestor origin/<branch> origin/dev && git push origin --delete <branch>`. Phiên chính đưa checkout về `dev` (`git switch dev && git pull --ff-only`) rồi xoá nhánh local bằng `git branch -d <branch>` — `-d`, không bao giờ `-D`, để git từ chối nếu nhánh còn commit chưa merge. Không xoá nhánh của PR chưa merge, bị đóng, hoặc đang chờ `ESCALATE`.

## 6b. Phát hành, ổn định và hotfix

Dùng skill `release-kit:git-release` (cấu hình ở `.claude/release-kit.json`; `CONTRIBUTING.md` § Release Process).

- **Cắt bản (Phase 1, trên `dev`):** skill cắt `release/vX.Y.Z` và soạn mục CHANGELOG ngay trên nhánh đó, không commit gì vào `dev`.
- **Ổn định:** lỗi tìm thấy khi QA được sửa trên nhánh `fix/*` cắt từ `release/vX.Y.Z` (`implementer`), PR vào chính nhánh release đó, rebase lên `origin/release/vX.Y.Z` trước khi review. `pr-reviewer` review như PR vào `dev` nhưng **không merge**: PASS được ghi lại và người dùng cho merge.
- **Phát hành (Phase 2):** skill chạy cổng bắt buộc, hỏi người dùng xác nhận, rồi tự mở và merge PR `release/vX.Y.Z → main` và gắn tag. Đó là cổng duy nhất của PR vào `main`.
- **Dọn nhánh `release/*` và `hotfix/*` sau khi merge vào `main`:** không dùng `gh pr merge --delete-branch`; người merge xoá nhánh trên remote chỉ sau khi xác nhận nhánh đã nằm trong `origin/main`: `git fetch --prune origin && git merge-base --is-ancestor origin/<branch> origin/main && git push origin --delete <branch>`, rồi xoá nhánh local bằng `git branch -d <branch>` — `-d`, không bao giờ `-D`.
- **Hotfix:** `implementer` sửa trên `hotfix/<tên>` cắt từ `main`; phiên chính chạy `code-reviewer` trên diff `origin/main...hotfix/<tên>`; rồi `git-release` lo CHANGELOG, cổng bắt buộc, PR vào `main` (người dùng xác nhận) và tag.
- **Đưa `main` về `dev` sau mỗi lần phát hành hoặc hotfix:** không push thẳng vào `dev`. `git-release` tự mở PR `chore/reconcile-main-vX.Y.Z` vào `dev` (`devWrites: "pr"`) và không merge; người dùng cho merge. PR này là ngoại lệ của quy tắc rebase: nó chính là một merge của `main`.
- **Phát hành nhanh (`quick`, `dev → main`, không có giai đoạn ổn định):** CHANGELOG đi qua PR `chore/release-vX.Y.Z-changelog` vào `dev`, merge bằng `/git-accept` (`devMergeCommand`), rồi skill mới mở PR vào `main`. Chỉ dùng khi bạn yêu cầu rõ `quick` và không cần QA riêng.
- **Cấu hình liên quan** trong `.claude/release-kit.json`: `devWrites: "pr"`, `devMergeCommand: "/git-accept"`, `pushAfterRebase: "force-with-lease"` (`release-kit:git-sync` và `git-mr` tự push sau khi rebase, chỉ ghi đè đúng head cũ của nhánh), `prExtraSections` (các mục mô tả PR mà quy trình yêu cầu; `release-kit:git-mr` tạo sẵn).

## 7. Truy vết

Mã yêu cầu đi suốt chuỗi: PRD (`PRDxx-FR-nn`) → tiêu đề bảng thiết kế → mục tài liệu kỹ thuật → task trong kế hoạch → test → dòng `PRD:` của feature-map → mô tả PR. Một mã không có hạ nguồn, hoặc một thành phần không có mã thượng nguồn, là một phát hiện ở bất kỳ khâu review nào.

## 8. Lỗi lặp và retro cuối module

Cùng một góp ý xuất hiện hai lần thì không thêm quy tắc văn bản nữa: biến nó thành lint, kiểm thử kiến trúc hoặc generator (`AGENTS.md`).

**Retro cuối module.** Khi PR cuối của một module đã merge vào `dev`, và trước khi đánh *Merged* trong `progress.md`, phiên chính chạy retro:
1. Phân loại các commit `fix` của module theo nguyên nhân gốc (một agent Sonnet đọc `git log` và `git show` của các nhánh module).
2. Ghi mỗi nhóm nguyên nhân vào `docs/agent-error-log.md`, và xử lý các ý trong mục *Chờ retro* của tệp đó.
3. Nhóm gặp từ hai lần trở lên thì biến thành kiểm tra tự động (việc cỡ S cho `implementer`), hoặc ghi lý do không làm được vào mục đó. Mục đã tự động hoá ghi "Đã chuyển thành: …".
4. Ghi số đo của module vào `progress.md` § Số đo.
5. Gom các thay đổi quy trình của module vào một PR quy trình.

**Đóng băng quy trình giữa module.** Thay đổi quy trình (tài liệu này, `AGENTS.md`, `.claude/agents/`, `.claude/rules/`) chỉ áp dụng ở retro. Giữa module chỉ sửa lỗi quy trình đang chặn việc, kèm lý do trong một mục ở §9. Ý cải tiến phát sinh giữa module được ghi vào mục *Chờ retro* của `docs/agent-error-log.md`.

**Mỗi quy tắc một nơi.** Mỗi quy tắc có một tệp sở hữu; nơi khác chỉ trỏ tới nó bằng `tệp § mục`, không chép lại nội dung. `AGENTS.md` là mục lục và chỉ giữ những quy tắc mọi phiên cần ngay. Nếu tham chiếu `§` hỏng xuất hiện từ hai lần, thêm một script kiểm tra vào `scripts/test-gate.sh`.

## 9. Quyết định quy trình

Quyết định về cách làm việc (agent, cổng duyệt, review) ghi ở đây, mới nhất ở trên; quyết định kỹ thuật theo `AGENTS.md` § Decision Log.

### 2026-11-12 — Codex dùng chung nội dung Claude, không giữ bản prompt thứ hai

Codex không nạp `paths:` của rule, file agent, slash command hay `settings.json` của Claude. Chép prompt sang cấu hình Codex sẽ lệch ngay lần sửa đầu tiên, còn ghi quy tắc git vào `AGENTS.md` thì không chặn được lệnh. Vì vậy `.claude/` vẫn là nguồn nội dung; Codex chỉ có lớp mỏng trỏ về nguồn đó (agent, skill, hook gọi cùng script guard). Phương án loại: duy trì hai bộ prompt, và coi hướng dẫn trong `AGENTS.md` là đủ thay cho hook.

### 2026-11-12 — Retro module 00: mỗi phase một PR, retro bắt buộc, đóng băng quy trình
Đánh giá quy trình sau module 00 cho thấy:
- PR #32 gom cả 8 phase: 607 tệp, +61.779/−10.594 dòng, 100 commit. Không ai review hết được, nên cổng người dùng ở §3 chỉ còn hình thức.
- 42 trên 158 commit của nhánh là `fix`, trong đó bảy nhóm nguyên nhân lặp lại (`docs/agent-error-log.md`, mục 2026-11-12).
- Nhật ký lỗi không có mục mới từ 2026-10-19 và chưa mục nào được tự động hoá: quy tắc ở §8 chưa có bước nào ép thực hiện.
- Khoảng 10 quyết định quy trình trong 5 ngày; kế hoạch mỗi phase dài 400–900 dòng, phần lớn là mã viết sẵn.

Người dùng yêu cầu áp dụng các cải tiến; SA (thay người dùng) quyết chi tiết:
- Mỗi phase một PR, giới hạn mềm 1.500 dòng mã, xếp chồng tối đa một tầng (§6).
- Kế hoạch không chép mã hoàn chỉnh; PRD module thường nhắm khoảng 500 dòng (§5b).
- Retro bắt buộc cuối module, đóng băng quy trình giữa module, mỗi quy tắc một nơi (§8).
- Bốn số đo mỗi module trong `progress.md`; bỏ số token vì không đo chính xác được.
- PR #32 vẫn là một PR: tách sau 100 commit tốn hơn lợi ích, vì một số fix của phase 1 và 3 nằm cuối nhánh. Thay vào đó `appsec` (Opus) review phần bảo mật, `pr-reviewer` review phần còn lại, và người dùng nhận một checklist rủi ro thay vì cả diff.

Phương án loại: tách PR #32 thành 8 PR xếp chồng (rebase theo phase dễ hỏng, phải chạy lại CI từng phần); kiểm tra CI cho mọi tham chiếu `§` ngay bây giờ (chưa có bằng chứng tham chiếu hỏng lặp lại); giới hạn cứng 400 dòng cho PRD (dễ thành cắt phạm vi).

### 2026-10-26 — Tăng tốc chạy kế hoạch trong một bản checkout
Người dùng thấy chạy kế hoạch còn chậm, nhưng muốn giữ nguyên quy tắc dự án. Quy tắc chung (gộp task nhỏ, cho review chạy chồng lên task kế tiếp, brief mang đủ phán quyết, nhịp chạy test) nằm trong `~/.claude/CLAUDE.md` § Subagent-driven execution pacing. Ở dự án này còn thêm:
- Mỗi lúc chỉ một agent build backend, vì các agent dùng chung `obj/` (`-m:1`). Hai task backend chạy nối tiếp nhau. Một task backend và một task frontend được chạy song song.
- Trong lúc làm, agent chỉ chạy project test bị ảnh hưởng. Trước khi commit, agent chạy `backend/scripts/test.sh` hoặc `bun run --cwd frontend test:run` một lần.
- Chỉ phiên chính chạy `scripts/test-gate.sh`, ở cuối phase và sau lượt sửa cuối.

Phương án loại: hai luồng song song bằng git worktree. Mỗi luồng phải cài và build lại từ đầu, các tệp dùng chung (`locales`, `router`, `feature-map.md`) phải gộp tay, và cách này trái với `AGENTS.md` § Branch & worktree. Chỉ dùng khi người dùng duyệt riêng.

### 2026-10-26 — Chọn model theo việc, mặc định Sonnet
Người dùng thấy các agent chạy ổn nhưng tốn kém. Ở module 00, gần như mọi implementer và reviewer chạy Opus, mỗi agent tốn 120–290 nghìn token. Vì vậy `implementer` và `code-reviewer` khai `model: sonnet` trong frontmatter, để một lượt gọi không ghi model cũng không thành Opus. Phiên chính chỉ đặt Opus qua tham số `model` cho:
- reviewer của task đụng tới đăng nhập, token, cookie, khoá tổ chức, kiểm quyền hoặc lọc theo tổ chức; implementer của các task này vẫn chạy Sonnet;
- review cuối mỗi phase.

Ngoài ra:
- Vòng sửa giao cho một implementer Sonnet mới, không gọi lại agent cũ, vì agent được gọi lại giữ model của nó. Chỉ khi sửa lần thứ hai vẫn chưa đạt mới chuyển sang Opus.
- Task cơ học (export contract, feature map, tài liệu, sửa chỉ trong test) không có review riêng; phiên chính tự xem diff.
- Chỉ task bảo mật mới cài lỗi để thử test, khoảng 5 lỗi.

Phương án loại: chỉ nhắc phiên chính chọn model mỗi lần gọi. Một lần quên thì agent đó chạy Opus.

### 2026-10-23 — `solution-architect` trả lời câu hỏi thay người dùng
Người dùng thấy bị hỏi quá nhiều: PRD-00 và thiết kế module 00 đã có hơn 80 câu hỏi, phần lớn được chấp nhận theo đề xuất. Từ nay mọi câu hỏi của agent đi tới `solution-architect` trước. Agent này đóng vai một SA dày dặn kinh nghiệm, chuyên Vue 3 và dashboard, ưu tiên cách đơn giản nhất thật sự cần, SOLID và Clean Architecture, giao diện tối giản và cân đối. Nó trả lời thay người dùng và chỉ chuyển lên những câu thuộc danh sách người dùng phải quyết:
- duyệt tài liệu và kế hoạch;
- các lệnh mà quy tắc dành cho người dùng;
- tiền và pháp lý;
- hướng sản phẩm, hoặc đổi một quyết định người dùng đã đưa;
- nới quy tắc bảo mật;
- lựa chọn khó đảo ngược mà không đoán được ý người dùng.

Câu trả lời của SA được ghi nguồn "SA (thay người dùng)", để người dùng soát lại khi duyệt tài liệu. SA quyết theo bối cảnh hệ thống trong `docs/process/system-context.md` (quy mô, hạ tầng, giới hạn). Mỗi lần người dùng bác một câu trả lời của SA, phiên chính ghi bài học vào `.claude/sa-calibration.md` trong cùng PR với tài liệu được sửa; SA đọc tệp này trước khi trả lời. Phương án loại: để `product-manager` tự ghi nhiều mặc định hơn. Cách này không có người chịu trách nhiệm cho câu trả lời, và câu hỏi về thiết kế, kỹ thuật vẫn phải hỏi người dùng.

### 2026-10-22 — Soạn PRD: khoanh phạm vi, chỉ hỏi điều người dùng phải quyết, BA review sớm
Bản nháp đầu của PRD-00 đi qua 8 phiên bản và 78 câu hỏi; người dùng chấp nhận đề xuất ở 77 câu, PRD phình tới 246 AC vì quyết luôn chi tiết của PRD-03/04/05/07, và lỗi nặng nhất (đường leo quyền có sẵn trong nền tảng) chỉ lộ ra khi BA review ở bản 0.5. Vì vậy:
- Mỗi PRD chỉ quyết phần của module mình; phần thuộc module khác chuyển vào §12 *Chuyển cho PRD khác*.
- Câu hỏi mở chỉ dành cho điều người dùng phải quyết, tối đa khoảng 8 câu mỗi lượt; phần còn lại, kể cả mức ưu tiên, là *Mặc định đề xuất*, được duyệt một lần cùng PRD.
- BA review ngay bản nháp đầu, và báo cáo được giữ lại cho lần review sau.
- Phiên chính trình hệ quả ngay khi một câu trả lời chạm bảo mật hay tách dữ liệu, trước khi chuyển cho agent viết.
- Trong bản nháp, mục bị bỏ được xoá thẳng thay vì gạch `~~Removed~~`; vẫn không đánh số lại và không dùng lại mã. Gạch bỏ chỉ áp dụng từ khi PRD được duyệt lần đầu.
- Bản nháp nằm trên nhánh riêng với một PR nháp từ đầu (§ Commit tài liệu), để báo cáo review được lưu thành comment.
- Mỗi PR một mục đích (§ Quyền push và mở PR).

Phương án loại: giữ nguyên quy trình và chỉ nhắc agent hỏi ít hơn (không có tiêu chí để phân loại, lặp lại vẫn xảy ra).

### 2026-10-22 — Bật lại `quick` qua PR khi release-kit hỗ trợ
release-kit v2.0.2 thêm `devWrites: "pr"` và `devMergeCommand`, nên phát hành nhanh và việc đưa `main` về `dev` không còn ghi thẳng vào `dev`; `quick` được bật lại. `pushAfterRebase` và `prExtraSections` cho phép dùng `git-sync`/`git-mr` đúng quy trình rebase và mẫu mô tả PR. Thay thế mục "Phát hành theo release-kit" ở điểm tắt `quick`.

### 2026-10-22 — Agent review không dùng skill `code-review`
Skill này chạy một lượt review nền có thể kết thúc sau khi agent đã kết luận. Ở PR #7, `pr-reviewer` merge rồi lượt nền mới báo một lỗi mất dữ liệu. Lỗi lặp hai lần (PR #4, #7) nên đổi thành quy tắc: agent tự review trong lượt chạy và chỉ merge khi mọi kiểm tra đã xong. Phương án loại: giữ skill và chờ (agent không có cách chờ đáng tin).

### 2026-10-22 — Phát hành theo release-kit, không ghi thẳng vào `dev`
CHANGELOG được dựng từ mục `## Changelog` của từng PR, nên mọi PR phải có mục này. Fix trong giai đoạn ổn định và hotfix cũng đi qua review; đưa `main` về `dev` là một PR thay vì push thẳng. Chế độ `quick` tạm tắt vì nó commit thẳng vào `dev`, cho đến khi plugin có tuỳ chọn đi qua PR. Phương án loại: sửa `CHANGELOG.md` bằng tay trong mỗi PR (xung đột giữa các nhánh song song).

### 2026-10-22 — Xoá nhánh ngay sau khi merge
Nhánh đã merge để lại trên remote và local làm danh sách nhánh rối và dễ rebase nhầm. Người merge xoá nhánh remote sau khi xác nhận nó đã nằm trong `dev`; nhánh local xoá bằng `git branch -d` để git tự chặn nếu còn commit chưa merge. Phương án loại: `gh pr merge --delete-branch` (nó xoá luôn nhánh local và đổi checkout dùng chung giữa các agent).

### 2026-10-22 — Merge từng PR một, rebase trước, giữ merge commit
Nhiều nhánh song song merge chồng nhau làm lịch sử `dev` rối. Mỗi lúc chỉ một PR được review và merge, nhánh luôn rebase lên `dev` mới nhất trước khi review, và merge bằng `--merge` để vẫn thấy ranh giới từng PR và revert cả PR bằng một lệnh. Phương án loại: `--rebase` (mất ranh giới PR) và `--squash` (mất commit theo task mà review từng task dùng).

### 2026-10-22 — Lệch thiết kế chia hai mức; tài liệu commit khi được duyệt
`implementer` không được sửa tài liệu kỹ thuật, nếu không nó tự hợp thức hoá chỗ lệch của chính mình. Lệch nhỏ chỉ khai báo trong PR để không phải duyệt lại vì chi tiết nội bộ; lệch lớn chạm vào thứ người dùng đã duyệt nên phải duyệt lại. Tài liệu commit khi được duyệt để `pr-reviewer` đối chiếu với bản đúng. Phương án loại: để `implementer` tự sửa tài liệu kỹ thuật.

### 2026-10-22 — Review và merge vào `dev` do agent gác cổng, không làm bằng tay
Không có tài khoản reviewer thứ hai và tác giả không tự duyệt được PR của mình, nên `pr-reviewer` review theo quy tắc repo, PRD và thiết kế, tối đa 3 lượt, và tự merge khi đạt. Xác thực, phiên, quyền, CI, file quy tắc agent và migration vẫn cần người dùng. Đánh đổi: cổng chạy phía client nên ép được quy trình, không phải kiểm soát truy cập. Phương án loại: bật branch protection bắt buộc review (không ai duyệt được).

### 2026-10-22 — Quyết định ghi theo phạm vi, không duyệt riêng
Quyết định module nằm ở §12 tài liệu kỹ thuật, được duyệt cùng tài liệu; quyết định nền tảng ghi vào `.claude/decisions.md` trong cùng PR, chỉ được thêm; quyết định quy trình ghi ở đây. Phương án loại: một cổng duyệt riêng cho mọi mục (chậm, trùng với duyệt tài liệu) và bỏ hẳn `decisions.md` (quyết định nền tảng mất chỗ ở).

## 10. Tiến độ module

Bảng tiến độ nằm ở `docs/process/progress.md`: mỗi module một dòng, mỗi giai đoạn một cột. Phiên chính là người duy nhất cập nhật bảng này: trong PR tài liệu khi PRD hoặc tài liệu kỹ thuật được duyệt, trong PR tính năng cho các cột *Plan* và *Code*, và đánh *Merged* ở PR tài liệu tiếp theo của module, chỉ sau khi retro cuối module (§8) xong. Bảng *Số đo* trong cùng tệp ghi bốn số mỗi module: số ngày lịch (từ bản nháp PRD đầu tiên tới khi PR cuối merge), số PR, tỉ lệ commit `fix` (`git log`), và số câu hỏi tới người dùng.
