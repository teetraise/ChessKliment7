using System;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ChessClient
{
    // Те же модели данных, что и на сервере
    public class ChessMove
    {
        public string SessionId { get; set; } = "";
        public string From { get; set; } = "";
        public string To { get; set; } = "";
        public string Piece { get; set; } = "";
        public DateTime Timestamp { get; set; }
    }

    public class AvatarPosition
    {
        public string SessionId { get; set; } = "";
        public float X { get; set; }
        public float Y { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class ChatMessage
    {
        public string SessionId { get; set; } = "";
        public string PlayerName { get; set; } = "";
        public string Message { get; set; } = "";
        public DateTime Timestamp { get; set; }
    }

    public class ChessClient
    {
        private string _sessionId = "";
        private string _playerName = "";
        private readonly List<(string host, int tcpPort, int udpPort, int wsPort)> _servers = new();
        private int _currentServerIndex = -1;
        
        private TcpClient? _tcpClient;
        private UdpClient? _udpClient;
        private UdpClient? _udpReceiver;
        private ClientWebSocket? _webSocket;
        
        private bool _isConnected = false;
        private readonly List<string> _eventHistory = new();

        public async Task StartAsync()
        {
            Console.WriteLine("♟️  Шахматный клиент v1.0 (macOS)");
            Console.WriteLine("==================================");

            // Генерируем уникальный ID сессии
            _sessionId = Guid.NewGuid().ToString("N")[..8];
            
            Console.Write("👤 Введите ваше имя: ");
            _playerName = Console.ReadLine() ?? "Игрок";

            // Добавляем серверы (можно добавить несколько)
            AddDefaultServers();

            bool exit = false;
            while (!exit)
            {
                Console.WriteLine("\n📋 Меню:");
                Console.WriteLine("1. Показать список серверов");
                Console.WriteLine("2. Добавить новый сервер");
                Console.WriteLine("3. Подключиться к серверу");
                Console.WriteLine("4. Отключиться от сервера");
                Console.WriteLine("5. Сделать ход");
                Console.WriteLine("6. Отправить сообщение в чат");
                Console.WriteLine("7. Переместить аватар");
                Console.WriteLine("8. Показать историю событий");
                Console.WriteLine("0. Выйти");
                
                Console.Write("Выберите действие: ");
                string? choice = Console.ReadLine();

                try
                {
                    switch (choice)
                    {
                        case "1":
                            ShowServers();
                            break;
                        case "2":
                            AddServer();
                            break;
                        case "3":
                            await ConnectToServerAsync();
                            break;
                        case "4":
                            await DisconnectAsync();
                            break;
                        case "5":
                            await MakeMoveAsync();
                            break;
                        case "6":
                            await SendChatMessageAsync();
                            break;
                        case "7":
                            await MoveAvatarAsync();
                            break;
                        case "8":
                            ShowEventHistory();
                            break;
                        case "0":
                            exit = true;
                            break;
                        default:
                            Console.WriteLine("❌ Неверный выбор!");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Ошибка: {ex.Message}");
                }
            }

            await DisconnectAsync();
            Console.WriteLine("👋 До свидания!");
        }

        private void AddDefaultServers()
        {
            _servers.Add(("localhost", 8080, 8081, 8082));
            _servers.Add(("127.0.0.1", 8080, 8081, 8082));
        }

        private void ShowServers()
        {
            Console.WriteLine("\n🌐 Список серверов:");
            for (int i = 0; i < _servers.Count; i++)
            {
                var server = _servers[i];
                string status = _currentServerIndex == i ? " [ПОДКЛЮЧЕН]" : "";
                Console.WriteLine($"{i + 1}. {server.host}:{server.tcpPort}{status}");
            }
        }

        private void AddServer()
        {
            Console.Write("🌐 Введите IP адрес сервера: ");
            string? host = Console.ReadLine();
            
            Console.Write("🔌 Введите TCP порт: ");
            if (int.TryParse(Console.ReadLine(), out int tcpPort))
            {
                Console.Write("📡 Введите UDP порт: ");
                if (int.TryParse(Console.ReadLine(), out int udpPort))
                {
                    Console.Write("🌐 Введите WebSocket порт: ");
                    if (int.TryParse(Console.ReadLine(), out int wsPort))
                    {
                        _servers.Add((host ?? "localhost", tcpPort, udpPort, wsPort));
                        Console.WriteLine("✅ Сервер добавлен!");
                    }
                    else
                    {
                        Console.WriteLine("❌ Неверный WebSocket порт!");
                    }
                }
                else
                {
                    Console.WriteLine("❌ Неверный UDP порт!");
                }
            }
            else
            {
                Console.WriteLine("❌ Неверный TCP порт!");
            }
        }

        private async Task ConnectToServerAsync()
        {
            if (_isConnected)
            {
                Console.WriteLine("⚠️  Вы уже подключены к серверу!");
                return;
            }

            ShowServers();
            Console.Write("Выберите номер сервера для подключения: ");
            
            if (int.TryParse(Console.ReadLine(), out int serverIndex) && 
                serverIndex >= 1 && serverIndex <= _servers.Count)
            {
                var server = _servers[serverIndex - 1];
                _currentServerIndex = serverIndex - 1;
                
                Console.WriteLine($"🔗 Подключение к {server.host}...");

                // Подключение TCP для ходов
                await ConnectTcpAsync(server.host, server.tcpPort);
                
                // Подключение UDP для позиций аватаров
                ConnectUdp(server.host, server.udpPort);
                
                // Подключение WebSocket для чата и событий
                await ConnectWebSocketAsync(server.host, server.wsPort);

                _isConnected = true;
                Console.WriteLine("✅ Подключение установлено!");
                
                // Запускаем прослушивание сообщений
                _ = Task.Run(ListenTcpAsync);
                _ = Task.Run(ListenUdpAsync);
                _ = Task.Run(ListenWebSocketAsync);
            }
            else
            {
                Console.WriteLine("❌ Неверный номер сервера!");
            }
        }

        private async Task ConnectTcpAsync(string host, int port)
        {
            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(host, port);
            
            // Регистрируемся на сервере
            string registerMessage = $"REGISTER:{_sessionId}|{_playerName}\n";
            byte[] data = Encoding.UTF8.GetBytes(registerMessage);
            await _tcpClient.GetStream().WriteAsync(data, 0, data.Length);
            
            Console.WriteLine("🔗 TCP подключение установлено");
        }

        private void ConnectUdp(string host, int port)
        {
            // Исправленная версия для macOS
            _udpClient = new UdpClient();
            _udpClient.Connect(host, port); // Синхронное подключение
            
            // Создаем отдельный UDP клиент для приема сообщений
            try
            {
                _udpReceiver = new UdpClient(port + 1000); // Используем другой порт для приема
            }
            catch (SocketException)
            {
                // Если порт занят, попробуем другой
                _udpReceiver = new UdpClient(port + 1001);
            }
            
            Console.WriteLine("📡 UDP подключение установлено");
        }

        private async Task ConnectWebSocketAsync(string host, int port)
        {
            _webSocket = new ClientWebSocket();
            var uri = new Uri($"ws://{host}:{port}/?sessionId={_sessionId}");
            await _webSocket.ConnectAsync(uri, CancellationToken.None);
            
            Console.WriteLine("🌐 WebSocket подключение установлено");
        }

        private async Task ListenTcpAsync()
        {
            if (_tcpClient?.GetStream() == null) return;

            byte[] buffer = new byte[1024];
            var stream = _tcpClient.GetStream();

            try
            {
                while (_isConnected && _tcpClient.Connected)
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;

                    string response = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Console.WriteLine($"📩 TCP: {response}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка TCP прослушивания: {ex.Message}");
            }
        }

        private async Task ListenUdpAsync()
        {
            if (_udpReceiver == null) return;

            try
            {
                while (_isConnected)
                {
                    var result = await _udpReceiver.ReceiveAsync();
                    string message = Encoding.UTF8.GetString(result.Buffer);
                    
                    if (message.StartsWith("POSITION:"))
                    {
                        var positionData = message.Substring(9);
                        var position = JsonSerializer.Deserialize<AvatarPosition>(positionData);
                        
                        if (position != null && position.SessionId != _sessionId)
                        {
                            Console.WriteLine($"👤 Позиция аватара: ({position.X:F1}, {position.Y:F1})");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка UDP прослушивания: {ex.Message}");
            }
        }

        private async Task ListenWebSocketAsync()
        {
            if (_webSocket == null) return;

            byte[] buffer = new byte[1024];

            try
            {
                while (_isConnected && _webSocket.State == WebSocketState.Open)
                {
                    var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }
                    else if (result.MessageType == WebSocketMessageType.Text)
                    {
                        string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        await ProcessWebSocketMessage(message);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка WebSocket прослушивания: {ex.Message}");
            }
        }

        private async Task ProcessWebSocketMessage(string message)
        {
            try
            {
                var data = JsonSerializer.Deserialize<JsonElement>(message);
                
                if (data.TryGetProperty("type", out var typeElement))
                {
                    string type = typeElement.GetString() ?? "";
                    
                    switch (type)
                    {
                        case "move":
                            if (data.TryGetProperty("player", out var playerElement) &&
                                data.TryGetProperty("move", out var moveElement))
                            {
                                var player = playerElement.GetString();
                                var move = JsonSerializer.Deserialize<ChessMove>(moveElement.GetRawText());
                                Console.WriteLine($"♟️  {player} сделал ход: {move?.From} → {move?.To} ({move?.Piece})");
                            }
                            break;
                            
                        case "chat":
                            if (data.TryGetProperty("player", out var chatPlayerElement) &&
                                data.TryGetProperty("message", out var chatMessageElement))
                            {
                                var player = chatPlayerElement.GetString();
                                var chatMsg = chatMessageElement.GetString();
                                Console.WriteLine($"💬 {player}: {chatMsg}");
                            }
                            break;
                            
                        case "event":
                            if (data.TryGetProperty("message", out var eventMessageElement))
                            {
                                var eventMsg = eventMessageElement.GetString();
                                Console.WriteLine($"🎮 СОБЫТИЕ: {eventMsg}");
                                _eventHistory.Add($"{DateTime.Now:HH:mm:ss} - {eventMsg}");
                            }
                            break;
                    }
                }
                else
                {
                    // Простое текстовое сообщение
                    Console.WriteLine($"🌐 {message}");
                }
                
                await Task.CompletedTask; // Убираем warning
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка обработки WebSocket сообщения: {ex.Message}");
                Console.WriteLine($"🌐 {message}"); // Выводим как есть
            }
        }

        private async Task DisconnectAsync()
        {
            if (!_isConnected) return;

            _isConnected = false;
            _currentServerIndex = -1;

            try
            {
                _tcpClient?.Close();
                _udpClient?.Close();
                _udpReceiver?.Close();
                
                if (_webSocket?.State == WebSocketState.Open)
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnecting", CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка при отключении: {ex.Message}");
            }

            Console.WriteLine("🔌 Отключен от сервера");
        }

        private async Task MakeMoveAsync()
        {
            if (!_isConnected || _tcpClient?.Connected != true)
            {
                Console.WriteLine("❌ Не подключен к серверу!");
                return;
            }

            Console.WriteLine("♟️  Введите ход в формате:");
            Console.WriteLine("   Откуда (например: e2): ");
            string? from = Console.ReadLine()?.ToLower();
            
            Console.WriteLine("   Куда (например: e4): ");
            string? to = Console.ReadLine()?.ToLower();
            
            Console.WriteLine("   Фигура (например: пешка): ");
            string? piece = Console.ReadLine();

            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to) || string.IsNullOrEmpty(piece))
            {
                Console.WriteLine("❌ Все поля должны быть заполнены!");
                return;
            }

            var move = new ChessMove
            {
                SessionId = _sessionId,
                From = from,
                To = to,
                Piece = piece,
                Timestamp = DateTime.Now
            };

            string moveJson = JsonSerializer.Serialize(move);
            string message = $"MOVE:{moveJson}\n";
            
            byte[] data = Encoding.UTF8.GetBytes(message);
            await _tcpClient.GetStream().WriteAsync(data, 0, data.Length);
            
            Console.WriteLine($"✅ Ход отправлен: {from} → {to}");
        }

        private async Task SendChatMessageAsync()
        {
            if (!_isConnected || _webSocket?.State != WebSocketState.Open)
            {
                Console.WriteLine("❌ WebSocket не подключен!");
                return;
            }

            Console.Write("💬 Введите сообщение: ");
            string? message = Console.ReadLine();

            if (string.IsNullOrEmpty(message))
            {
                Console.WriteLine("❌ Сообщение не может быть пустым!");
                return;
            }

            var chatMessage = new ChatMessage
            {
                SessionId = _sessionId,
                Message = message,
                Timestamp = DateTime.Now
            };

            string chatJson = JsonSerializer.Serialize(chatMessage);
            string wsMessage = $"CHAT:{chatJson}";
            
            byte[] data = Encoding.UTF8.GetBytes(wsMessage);
            await _webSocket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None);
            
            Console.WriteLine("✅ Сообщение отправлено");
        }

        private async Task MoveAvatarAsync()
        {
            if (!_isConnected || _udpClient == null)
            {
                Console.WriteLine("❌ UDP не подключен!");
                return;
            }

            Console.Write("👤 Введите X координату (0-7): ");
            if (!float.TryParse(Console.ReadLine(), out float x) || x < 0 || x > 7)
            {
                Console.WriteLine("❌ Неверная X координата!");
                return;
            }

            Console.Write("👤 Введите Y координату (0-7): ");
            if (!float.TryParse(Console.ReadLine(), out float y) || y < 0 || y > 7)
            {
                Console.WriteLine("❌ Неверная Y координата!");
                return;
            }

            var position = new AvatarPosition
            {
                SessionId = _sessionId,
                X = x,
                Y = y,
                Timestamp = DateTime.Now
            };

            string positionJson = JsonSerializer.Serialize(position);
            string message = $"AVATAR:{positionJson}";
            
            byte[] data = Encoding.UTF8.GetBytes(message);
            
            // Исправленная версия для macOS
            try
            {
                var result = await _udpClient.SendAsync(data, data.Length);
                Console.WriteLine($"✅ Аватар перемещен на позицию ({x}, {y})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка отправки UDP: {ex.Message}");
            }
        }

        private void ShowEventHistory()
        {
            Console.WriteLine("\n📜 История событий:");
            if (_eventHistory.Count == 0)
            {
                Console.WriteLine("   (нет событий)");
            }
            else
            {
                foreach (var eventMsg in _eventHistory.TakeLast(10)) // Показываем последние 10 событий
                {
                    Console.WriteLine($"   {eventMsg}");
                }
            }
        }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            var client = new ChessClient();
            await client.StartAsync();
        }
    }
}