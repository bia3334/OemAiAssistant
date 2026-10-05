# Kịch bản demo — Phase 2: OEM AI Fleet Console

**Thông điệp:** Phase 1 (app Windows + AI on-device) giữ nguyên. Phase 2 mở rộng thành nền tảng
web cho hãng OEM: mỗi laptop gửi telemetry về server, đối tác xem cả đội máy và hỏi AI về
từng máy.

```
[Laptop OEM] OemAi.Agent ──telemetry/2s──► [Server] OemAi.Api ──► AI (Foundry Local / Mock)
             (đọc phần cứng thật,                ├ lưu snapshot mới nhất từng máy
              dùng chung Infrastructure          └ Fleet Console (web) cho đối tác OEM
              với app WPF Phase 1)
```

## Chuẩn bị (5 phút trước demo)
1. **Dùng AI thật** (đã kiểm tra chạy được trên máy này: Phi-4-mini trên GPU, 0.4–3 s/câu):
   `foundry server status` → nếu model chưa load: `foundry model load Phi-4-mini-instruct-cuda-gpu:5`.
   AI thật trả lời **tiếng Nhật** khi hỏi bằng tiếng Nhật (đã thử: chẩn đoán đúng CPU/RAM/pin của từng máy).
   Dự phòng khi Foundry hỏng: `.\demo.ps1 -Mock` — **tránh dùng khi demo bằng tiếng Nhật**: AI luật offline
   luôn trả lời tiếng Việt và câu hỏi nối tiếp ra y hệt câu đầu.
2. `.\demo.ps1` — build, mở 2 cửa sổ (API, Agent) và trình duyệt ở http://localhost:5000.
3. Kiểm tra: dashboard có 7 máy (6 máy 模擬 + máy này 実機), cửa sổ Agent in dòng `OK`.
   Giao diện mặc định **日本語**; nút `日本語 | EN` ở góc phải trên để đổi ngôn ngữ (nhớ lựa chọn).
   Logo: chép file logo công ty vào `src/OemAi.Api/wwwroot/logo.svg`
   — có file thì logo tự hiện, không có thì ẩn.
4. **Hỏi khởi động 1 câu** (câu đầu tiên ~7 s vì phải dò Foundry), rồi bấm sang máy khác để xoá chat.

## Các bước trình bày (~5 phút)
| # | Thao tác | Nói gì |
|---|---|---|
| 1 | Mở app WPF (Phase 1) cạnh dashboard | "Đây là Phase 1: app Windows đọc phần cứng, AI chạy ngay trên máy." |
| 2 | Chỉ vào KPI + danh sách máy | "Phase 2: đối tác OEM thấy cả đội máy Dell, HP, Lenovo, Asus, Acer — máy nào cần chú ý lên đầu." |
| 3 | Lọc theo hãng (LENOVO) | "Mỗi OEM chỉ quan tâm máy của mình — sau này là multi-tenant." |
| 4 | Chọn máy `LIVE` (máy này), mở Task Manager / chạy tác vụ nặng | "Số liệu là thật, cập nhật mỗi 2 giây từ agent trên laptop." |
| 5 | Chọn HP EliteBook (CPU高負荷) → chip 「このデバイスの問題点は？」 | AI chẩn đoán bằng tiếng Nhật dựa trên telemetry của **đúng máy đó** (CPU 95% > ngưỡng 85%). |
| 6 | Hỏi tiếp 「動作を速くするには？」 | AI nhớ ngữ cảnh hội thoại, đưa danh sách bước xử lý. |
| 7 | Chọn ThinkPad X1 (メモリ逼迫) → 「このデバイスの問題点は？」; Zenbook → 「バッテリーの状態は？」; chỉ ThinkPad T14 (オフライン 3h) | AI và badge trên dashboard **luôn khớp nhau** (cùng ngưỡng cảnh báo). |
| 8 | Mở `/swagger` | "API mở cho đối tác tích hợp." |
| 9 | *(tuỳ chọn, LAN)* laptop thứ 2 chạy agent → máy mới hiện lên dashboard | Kịch bản hybrid thật. |

**Demo qua LAN:** máy chủ chạy `.\demo.ps1 -Url http://0.0.0.0:5000`; laptop khác chạy
`dotnet run --project src/OemAi.Agent -- --server http://<IP-máy-chủ>:5000`
(cho phép qua Windows Firewall khi được hỏi).

## Câu hỏi hay gặp
- **Sao không dùng web đọc phần cứng luôn?** Server không đọc được phần cứng máy người dùng →
  luôn cần agent trên máy (hybrid).
- **Dữ liệu lưu ở đâu?** Demo: bộ nhớ RAM của server (mất khi tắt). Bản thật: PostgreSQL (OA-3).
- **Bảo mật?** Demo chưa có xác thực. Bản thật: API key/chứng chỉ thiết bị (OA-5, OA-10), telemetry
  được kiểm tra đầu vào chống prompt-injection (đã có).
- **AI cloud?** Cùng interface `IAIService` — đổi sang Azure OpenAI là cấu hình (OA-4).
- **AI có bịa không?** Server tự tính cảnh báo theo ngưỡng (CPU ≥ 85%, RAM ≥ 90%, pin ≤ 20% không sạc)
  rồi đưa cho model → câu trả lời khớp với badge trên dashboard.
- **Tiếp theo?** Xem `docs/roadmap.md` (OA-3 → OA-14).

## Sự cố khi demo
| Triệu chứng | Xử lý |
|---|---|
| Chat báo `AI engine unavailable: …` | Đóng cửa sổ API, chạy lại `.\demo.ps1 -Mock` (cửa sổ Agent cứ để nguyên) |
| Bấm vào máy trong danh sách không ăn | Bấm lại (danh sách vẽ lại mỗi 2 s, thỉnh thoảng nuốt 1 click) |
| Không thấy máy 実機 (`LIVE`) | Xem cửa sổ Agent: `FAIL` → API chưa chạy / sai URL |
| Port 5000 bận | `.\demo.ps1 -Url http://localhost:5080` |
