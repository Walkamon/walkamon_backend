# Walkamon Presence Hub — Hướng dẫn tích hợp FE

## 1. Mục đích

Backend có một SignalR hub riêng để xác định trạng thái online của người dùng:

```text
https://api.walkamon.xyz/hubs/presence
```

`PresenceHub` phải được kết nối ngay sau khi login hoặc auto-login thành công và
được giữ trong toàn bộ thời gian người dùng sử dụng ứng dụng. Đây là kết nối
WebSocket chạy nền, không phải một màn hình.

Hub PvP cũ vẫn được giữ:

```text
https://api.walkamon.xyz/hubs/pvp-sprint
```

Phân chia trách nhiệm:

| Hub | Thời điểm kết nối | Chức năng |
| --- | --- | --- |
| `/hubs/presence` | Ngay sau login/auto-login | Online/offline, trạng thái bạn bè, lời mời và event cấp user |
| `/hubs/pvp-sprint` | Khi vào chức năng PvP hoặc được gán trận | Join match, ready và event realtime trong trận |

Một user được xem là online nếu còn ít nhất một connection ở một trong hai hub.
Đóng hub PvP không làm user offline khi PresenceHub vẫn còn kết nối.

## 2. Authentication

PresenceHub yêu cầu:

- JWT hợp lệ.
- JWT có role `User`.
- Claim `NameIdentifier` là `userId` hợp lệ.

FE không tự nối token vào URL. Dùng `accessTokenFactory`; SignalR client sẽ gửi
token dưới query parameter `access_token` khi negotiate/WebSocket connect.

Ví dụ với `signalr_netcore`:

```dart
final connection = HubConnectionBuilder()
    .withUrl(
      'https://api.walkamon.xyz/hubs/presence',
      options: HttpConnectionOptions(
        accessTokenFactory: () async => accessToken,
      ),
    )
    .withAutomaticReconnect()
    .build();

await connection.start();
```

Chỉ tạo một instance PresenceHub connection cho toàn ứng dụng. Không tạo một
connection mới cho mỗi màn hình hoặc mỗi lần widget rebuild.

## 3. Lifecycle bắt buộc

### Login thông thường

```text
POST login thành công
→ lưu JWT
→ đăng ký các SignalR handler
→ start PresenceHub
→ mở Home
```

Có thể mở Home trong lúc kết nối đang được thiết lập, nhưng UI danh sách bạn bè
phải refresh sau khi connection chuyển sang trạng thái connected.

### Auto-login khi mở lại app

```text
Đọc JWT đã lưu
→ xác nhận session/token còn dùng được
→ đăng ký SignalR handler
→ start PresenceHub
→ mở Home
```

Nếu người dùng vuốt tắt ứng dụng, Android sẽ đóng WebSocket và backend sẽ xem
user là offline. Khi mở lại app, auto-login phải start PresenceHub lại để user
trở thành online.

### App background và foreground

- `inactive` hoặc `paused`: không chủ động gọi `stop()`.
- `resumed`: gọi hàm `ensureConnected()`; chỉ start nếu connection chưa
  connected/connecting/reconnecting.
- Android có thể kill process khi app ở background. Khi app được mở lại, chạy
  lại luồng auto-login phía trên.
- Mất mạng: để automatic reconnect hoạt động.
- Reconnect thành công: backend tự đăng ký connection mới và tự thêm user vào
  group `user:{userId}`.

### Logout

```text
stop PresenceHub
→ stop SprintHub nếu đang kết nối
→ xóa JWT
→ về màn Login
```

Không dùng logout chỉ bằng cách xóa JWT trong khi connection cũ vẫn còn chạy.

## 4. Quản lý connection an toàn

FE nên có một singleton/application service, ví dụ:

```dart
class PresenceConnectionService {
  HubConnection? _connection;
  Future<void>? _connecting;

  Future<void> ensureConnected(String accessToken) async {
    final connection = _connection;
    if (connection?.state == HubConnectionState.Connected) return;

    if (_connecting != null) {
      await _connecting;
      return;
    }

    _connecting = _connect(accessToken);
    try {
      await _connecting;
    } finally {
      _connecting = null;
    }
  }

  Future<void> disconnect() async {
    final connection = _connection;
    _connection = null;
    if (connection != null) {
      await connection.stop();
    }
  }
}
```

Mục tiêu của `_connecting` là ngăn `login`, `resumed` và widget cùng gọi
`start()` nhiều lần.

Đăng ký handler trước khi gọi `start()` để không bỏ lỡ event đầu tiên.

## 5. Event `presence.changed`

Method name SignalR:

```text
presence.changed
```

Payload là JSON object, không phải chuỗi JSON lồng:

```json
{
  "eventId": "0c461949-74ba-4270-8f1c-c96f3490507a",
  "eventType": "presence.changed",
  "aggregateId": "c99db210-4170-f111-8478-000d3a862c93",
  "payload": {
    "userId": "c99db210-4170-f111-8478-000d3a862c93",
    "isOnline": true,
    "pvpAvailabilityCode": "available",
    "serverTime": "2026-07-30T18:30:00+07:00"
  }
}
```

FE cập nhật đúng friend có `userId == payload.userId`.

Giá trị `pvpAvailabilityCode`:

| Giá trị | Ý nghĩa |
| --- | --- |
| `offline` | Không còn connection SignalR |
| `available` | Online và không có activity PvP |
| `busy` | Online và đang có row trong `pvp_player_activities` |

Không suy luận `isOnline` từ `pvpAvailabilityCode` nếu response đã có trường
`isOnline`.

## 6. `GET /api/friends`

Response mỗi friend có:

```json
{
  "userId": "c99db210-4170-f111-8478-000d3a862c93",
  "username": "Thienne123",
  "isOnline": true,
  "pvpAvailabilityCode": "available"
}
```

Nguồn dữ liệu:

- `isOnline`: Presence tracker của SignalR, không phải một cột database.
- `pvpAvailabilityCode`: kết hợp presence với bảng
  `pvp_player_activities`.

Sau khi PresenceHub reconnect thành công, FE nên gọi lại `GET /api/friends` một
lần để đồng bộ authoritative state, sau đó tiếp tục cập nhật bằng
`presence.changed`.

## 7. Event cấp user trên PresenceHub

Backend gửi các event cấp user qua PresenceHub:

- `invite.created`
- `invite.declined`
- `invite.cancelled`
- `invite.expired`
- `match.assigned`
- Một số event kết thúc trận có `notifyUsers=true`

Ví dụ `invite.created`:

```json
{
  "eventId": "...",
  "eventType": "invite.created",
  "aggregateId": "<invitee-user-id>",
  "payload": {
    "inviteId": "...",
    "expiresAt": "2026-07-30T18:31:00+07:00"
  }
}
```

Khi nhận event invite, gọi:

```http
GET /api/pvp/sprint/invites
```

để lấy dữ liệu invite đầy đủ và authoritative.

Ví dụ `match.assigned`:

```json
{
  "eventId": "...",
  "eventType": "match.assigned",
  "aggregateId": "<user-id>",
  "payload": {
    "matchId": "...",
    "matchTypeCode": "ranked",
    "sourceCode": "matchmaking",
    "statusCode": "countdown",
    "countdownStartsAt": "2026-07-30T18:30:00+07:00",
    "countdownEndsAt": "2026-07-30T18:30:05+07:00",
    "readyExpiresAt": "2026-07-30T18:30:10+07:00",
    "lastEventSequence": 1,
    "serverTime": "2026-07-30T18:30:00+07:00"
  }
}
```

Khi nhận `match.assigned`:

1. Lấy `matchId`.
2. Kết nối SprintHub nếu chưa kết nối.
3. Gọi `JoinMatch(matchId)`.
4. Gọi `GET /api/pvp/sprint/matches/{matchId}` để đồng bộ state.
5. Tiếp tục xử lý sequence/event trong trận trên SprintHub.

## 8. Dedupe trong giai đoạn tương thích

Backend tạm thời phát event cấp user và `presence.changed` qua cả PresenceHub
và SprintHub để client cũ không bị hỏng.

Nếu FE đang kết nối đồng thời cả hai hub, cùng một event có thể được nhận hai
lần. FE bắt buộc dedupe bằng `eventId`.

Gợi ý:

- Giữ một `Set<String>` các `eventId` gần nhất.
- Nếu đã xử lý `eventId`, bỏ qua event lặp.
- Giới hạn cache, ví dụ 500–1000 event, để không tăng RAM vô hạn.

Event theo group match như `match.progress`, `match.settling` và
`match.finished` vẫn authoritative trên SprintHub và có thêm match sequence.

## 9. Trường hợp lỗi cần xử lý

| Tình huống | FE xử lý |
| --- | --- |
| JWT hết hạn | Refresh/login lại, sau đó tạo connection bằng token mới |
| `401/403` khi connect | Không retry vô hạn; đưa user về luồng xác thực |
| Mất mạng | Hiển thị trạng thái reconnecting và để automatic reconnect chạy |
| App resumed nhưng hub disconnected | Gọi `ensureConnected()` |
| Server vừa deploy/restart | Automatic reconnect; sau connected gọi lại friend list |
| Nhận invite nhưng GET invite chưa thấy | Retry ngắn vì event delivery là at-least-once |
| Nhận cùng event hai lần | Bỏ qua bằng `eventId` |

## 10. Acceptance test cho FE

1. Đăng nhập tài khoản A và B trên hai thiết bị.
2. Cả hai kết nối `/hubs/presence`.
3. A gọi `GET /api/friends` và thấy B `isOnline=true`.
4. B vuốt tắt app; đợi server phát hiện disconnect và kiểm tra B thành
   `offline`.
5. B mở lại app; auto-login reconnect và A nhận `presence.changed` với
   `isOnline=true`.
6. B vào queue hoặc nhận invite; A thấy B chuyển thành `busy`.
7. Trận/invite kết thúc; A thấy B trở lại `available`.
8. A gửi invite khi B đang ở màn Home; B vẫn nhận `invite.created`.
9. B được gán trận; nhận `match.assigned`, kết nối SprintHub và join match.
10. Kết nối đồng thời hai hub; xác nhận cùng `eventId` chỉ được xử lý một lần.

## 11. Kiểm tra log backend

Backend ghi log khi kết nối/ngắt kết nối với các trường:

```text
MethodName
Hub
UserId
ConnectionId
BecameOnline hoặc BecameOffline
```

Tìm trong Dozzle:

```text
SignalR connected
SignalR disconnected
PresenceHub
SprintHub
```

Nếu app đang mở nhưng `GET /api/friends` vẫn trả offline, kiểm tra trước tiên
xem log có dòng `Hub=PresenceHub` và đúng `UserId` hay không.
