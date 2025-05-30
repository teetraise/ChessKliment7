using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ChessServer
{
    // Модели данных
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

    public class WebMessage
    {
        public string Type { get; set; } = "";
        public string SessionId { get; set; } = "";
        public string PlayerName { get; set; } = "";
        public string Message { get; set; } = "";
        public ChessMove? Move { get; set; }
        public AvatarPosition? AvatarPosition { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public string From { get; set; } = "";
        public string To { get; set; } = "";
        public string Piece { get; set; } = "";
        public DateTime Timestamp { get; set; }
    }

    public class Player
    {
        public string SessionId { get; set; } = "";
        public string Name { get; set; } = "";
        public TcpClient? TcpClient { get; set; }
        public WebSocket? WebSocket { get; set; }
        public DateTime LastActivity { get; set; }
        public float AvatarX { get; set; }
        public float AvatarY { get; set; }
        public bool IsWebClient { get; set; }
    }

    public class ChessServer
    {
        private readonly ConcurrentDictionary<string, Player> _players = new();
        private readonly List<ChessMove> _gameHistory = new();
        private readonly List<ChatMessage> _chatHistory = new();
        
        private TcpListener? _tcpListener;
        private UdpClient? _udpListener;
        private HttpListener? _webSocketListener;
        
        private readonly int _tcpPort = 8080;
        private readonly int _udpPort = 8081;
        private readonly int _webSocketPort = 8082;
        
        private bool _isRunning = false;

        public async Task StartAsync()
        {
            _isRunning = true;
            Console.WriteLine("🏁 Запуск шахматного сервера (с поддержкой веб-клиентов)...");

            // Запуск TCP сервера для ходов
            _ = Task.Run(StartTcpServerAsync);
            
            // Запуск UDP сервера для позиций аватаров
            _ = Task.Run(StartUdpServerAsync);
            
            // Запуск WebSocket сервера для уведомлений и чата
            _ = Task.Run(StartWebSocketServerAsync);

            // Генерация игровых событий
            _ = Task.Run(GenerateGameEventsAsync);

            Console.WriteLine($"🎮 Сервер запущен:");
            Console.WriteLine($"   TCP (ходы): порт {_tcpPort}");
            Console.WriteLine($"   UDP (аватары): порт {_udpPort}");
            Console.WriteLine($"   WebSocket (чат/события): порт {_webSocketPort}");
            Console.WriteLine($"   Веб-интерфейс: http://localhost:{_webSocketPort}/chess.html");
            Console.WriteLine("Нажмите любую клавишу для остановки...");
            
            Console.ReadKey();
            await StopAsync();
        }

        private async Task StartTcpServerAsync()
        {
            _tcpListener = new TcpListener(IPAddress.Any, _tcpPort);
            _tcpListener.Start();
            
            Console.WriteLine($"🔗 TCP сервер запущен на порту {_tcpPort}");

            while (_isRunning)
            {
                try
                {
                    var tcpClient = await _tcpListener.AcceptTcpClientAsync();
                    _ = Task.Run(() => HandleTcpClientAsync(tcpClient));
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Ошибка TCP: {ex.Message}");
                }
            }
        }

        private async Task HandleTcpClientAsync(TcpClient tcpClient)
        {
            NetworkStream stream = tcpClient.GetStream();
            byte[] buffer = new byte[1024];
            
            try
            {
                while (tcpClient.Connected && _isRunning)
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;

                    string data = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    await ProcessTcpMessage(data, tcpClient);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка обработки TCP клиента: {ex.Message}");
            }
            finally
            {
                // Удаляем игрока при отключении
                var playerToRemove = _players.Values.FirstOrDefault(p => p.TcpClient == tcpClient);
                if (playerToRemove != null)
                {
                    _players.TryRemove(playerToRemove.SessionId, out _);
                    Console.WriteLine($"👋 Игрок {playerToRemove.Name} отключился");
                    await BroadcastToWebSockets($"Игрок {playerToRemove.Name} покинул игру");
                }
                
                tcpClient.Close();
            }
        }

        private async Task ProcessTcpMessage(string data, TcpClient tcpClient)
        {
            try
            {
                var lines = data.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.StartsWith("REGISTER:"))
                    {
                        var parts = line.Substring(9).Split('|');
                        if (parts.Length >= 2)
                        {
                            var sessionId = parts[0];
                            var playerName = parts[1];
                            
                            var player = new Player
                            {
                                SessionId = sessionId,
                                Name = playerName,
                                TcpClient = tcpClient,
                                LastActivity = DateTime.Now,
                                AvatarX = new Random().Next(0, 8),
                                AvatarY = new Random().Next(0, 8),
                                IsWebClient = false
                            };
                            
                            _players.TryAdd(sessionId, player);
                            Console.WriteLine($"👤 Зарегистрирован TCP игрок: {playerName} (ID: {sessionId})");
                            
                            // Отправляем подтверждение
                            await SendTcpResponse(tcpClient, $"REGISTERED:{sessionId}");
                            
                            // Уведомляем других игроков через WebSocket
                            await BroadcastToWebSockets($"Игрок {playerName} присоединился к игре!");
                        }
                    }
                    else if (line.StartsWith("MOVE:"))
                    {
                        var moveData = line.Substring(5);
                        var move = JsonSerializer.Deserialize<ChessMove>(moveData);
                        
                        if (move != null && _players.ContainsKey(move.SessionId))
                        {
                            await ProcessMove(move);
                            await SendTcpResponse(tcpClient, "MOVE_ACCEPTED");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка обработки TCP сообщения: {ex.Message}");
            }
        }

        private async Task SendTcpResponse(TcpClient client, string message)
        {
            try
            {
                if (client.Connected)
                {
                    byte[] data = Encoding.UTF8.GetBytes(message + "\n");
                    await client.GetStream().WriteAsync(data, 0, data.Length);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка отправки TCP ответа: {ex.Message}");
            }
        }

        private async Task StartUdpServerAsync()
        {
            _udpListener = new UdpClient(_udpPort);
            Console.WriteLine($"📡 UDP сервер запущен на порту {_udpPort}");

            while (_isRunning)
            {
                try
                {
                    var result = await _udpListener.ReceiveAsync();
                    _ = Task.Run(() => ProcessUdpMessage(result.Buffer, result.RemoteEndPoint));
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Ошибка UDP: {ex.Message}");
                }
            }
        }

        private async Task ProcessUdpMessage(byte[] data, IPEndPoint remoteEndPoint)
        {
            try
            {
                string message = Encoding.UTF8.GetString(data);
                
                if (message.StartsWith("AVATAR:"))
                {
                    var positionData = message.Substring(7);
                    var position = JsonSerializer.Deserialize<AvatarPosition>(positionData);
                    
                    if (position != null && _players.ContainsKey(position.SessionId))
                    {
                        await ProcessAvatarPosition(position);
                        await BroadcastUdpPosition(position, remoteEndPoint);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка обработки UDP сообщения: {ex.Message}");
            }
        }

        private async Task BroadcastUdpPosition(AvatarPosition position, IPEndPoint excludeEndPoint)
        {
            try
            {
                var positionJson = JsonSerializer.Serialize(position);
                byte[] data = Encoding.UTF8.GetBytes($"POSITION:{positionJson}");
                
                var broadcastEndPoint = new IPEndPoint(excludeEndPoint.Address, excludeEndPoint.Port + 1);
                await _udpListener!.SendAsync(data, data.Length, broadcastEndPoint);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка трансляции UDP позиции: {ex.Message}");
            }
        }

        private async Task StartWebSocketServerAsync()
        {
            _webSocketListener = new HttpListener();
            _webSocketListener.Prefixes.Add($"http://localhost:{_webSocketPort}/");
            _webSocketListener.Start();
            
            Console.WriteLine($"🌐 WebSocket сервер запущен на порту {_webSocketPort}");

            while (_isRunning)
            {
                try
                {
                    var context = await _webSocketListener.GetContextAsync();
                    
                    if (context.Request.IsWebSocketRequest)
                    {
                        _ = Task.Run(() => HandleWebSocketAsync(context));
                    }
                    else if (context.Request.Url?.AbsolutePath == "/chess.html")
                    {
                        // Возвращаем HTML файл
                        await ServeHtmlFile(context);
                    }
                    else
                    {
                        context.Response.StatusCode = 400;
                        context.Response.Close();
                    }
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Ошибка WebSocket: {ex.Message}");
                }
            }
        }

        private async Task ServeHtmlFile(HttpListenerContext context)
        {
            try
            {
                context.Response.ContentType = "text/html; charset=utf-8";
                const string htmlContent = "<!-- Здесь должен быть HTML контент веб-клиента -->";
                
                byte[] buffer = Encoding.UTF8.GetBytes(htmlContent);
                context.Response.ContentLength64 = buffer.Length;
                await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                context.Response.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка отдачи HTML: {ex.Message}");
            }
        }

        private async Task HandleWebSocketAsync(HttpListenerContext context)
        {
            WebSocket? webSocket = null;
            string sessionId = "";
            
            try
            {
                var wsContext = await context.AcceptWebSocketAsync(null);
                webSocket = wsContext.WebSocket;
                
                sessionId = context.Request.QueryString["sessionId"] ?? "";
                Console.WriteLine($"🌐 WebSocket подключен (SessionId: {sessionId})");

                byte[] buffer = new byte[1024];
                
                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                        break;
                    }
                    else if (result.MessageType == WebSocketMessageType.Text)
                    {
                        string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        await ProcessWebSocketMessage(message, webSocket, sessionId);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка обработки WebSocket: {ex.Message}");
            }
            finally
            {
                // Очищаем WebSocket из игрока
                if (!string.IsNullOrEmpty(sessionId) && _players.ContainsKey(sessionId))
                {
                    var player = _players[sessionId];
                    if (player.IsWebClient)
                    {
                        _players.TryRemove(sessionId, out _);
                        Console.WriteLine($"🌐 Веб-клиент отключен: {player.Name}");
                        await BroadcastToWebSockets($"Игрок {player.Name} покинул игру");
                    }
                    else
                    {
                        player.WebSocket = null;
                        Console.WriteLine($"🌐 WebSocket отключен для игрока: {player.Name}");
                    }
                }
            }
        }

        private async Task ProcessWebSocketMessage(string message, WebSocket webSocket, string sessionId)
        {
            try
            {
                var webMessage = JsonSerializer.Deserialize<WebMessage>(message);
                if (webMessage == null) return;

                switch (webMessage.Type.ToLower())
                {
                    case "register":
                        await HandleWebRegister(webMessage, webSocket);
                        break;
                        
                    case "tcp_move":
                        await HandleWebMove(webMessage);
                        break;
                        
                    case "chat":
                        await HandleWebChat(webMessage);
                        break;
                        
                    case "avatar_position":
                        await HandleWebAvatarPosition(webMessage);
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка обработки WebSocket сообщения: {ex.Message}");
            }
        }

        private async Task HandleWebRegister(WebMessage webMessage, WebSocket webSocket)
        {
            var player = new Player
            {
                SessionId = webMessage.SessionId,
                Name = webMessage.PlayerName,
                WebSocket = webSocket,
                LastActivity = DateTime.Now,
                AvatarX = new Random().Next(0, 8),
                AvatarY = new Random().Next(0, 8),
                IsWebClient = true
            };
            
            _players.TryAdd(webMessage.SessionId, player);
            Console.WriteLine($"👤 Зарегистрирован веб-игрок: {webMessage.PlayerName} (ID: {webMessage.SessionId})");
            
            await BroadcastToWebSockets($"Игрок {webMessage.PlayerName} присоединился к игре!");
        }

        private async Task HandleWebMove(WebMessage webMessage)
        {
            var move = new ChessMove
            {
                SessionId = webMessage.SessionId,
                From = webMessage.From,
                To = webMessage.To,
                Piece = webMessage.Piece,
                Timestamp = DateTime.Now
            };
            
            await ProcessMove(move);
        }

        private async Task HandleWebChat(WebMessage webMessage)
        {
            var chatMessage = new ChatMessage
            {
                SessionId = webMessage.SessionId,
                Message = webMessage.Message,
                Timestamp = DateTime.Now
            };
            
            if (_players.ContainsKey(webMessage.SessionId))
            {
                chatMessage.PlayerName = _players[webMessage.SessionId].Name;
                _chatHistory.Add(chatMessage);
                Console.WriteLine($"💬 Чат от {chatMessage.PlayerName}: {chatMessage.Message}");
                
                var chatNotification = JsonSerializer.Serialize(new
                {
                    type = "chat",
                    player = chatMessage.PlayerName,
                    message = chatMessage.Message,
                    timestamp = chatMessage.Timestamp
                });
                
                await BroadcastToWebSockets(chatNotification);
            }
        }

        private async Task HandleWebAvatarPosition(WebMessage webMessage)
        {
            var position = new AvatarPosition
            {
                SessionId = webMessage.SessionId,
                X = webMessage.X,
                Y = webMessage.Y,
                Timestamp = DateTime.Now
            };
            
            await ProcessAvatarPosition(position);
        }

        private async Task ProcessMove(ChessMove move)
        {
            move.Timestamp = DateTime.Now;
            _gameHistory.Add(move);
            
            if (_players.ContainsKey(move.SessionId))
            {
                var player = _players[move.SessionId];
                Console.WriteLine($"♟️  Ход от {player.Name}: {move.From} → {move.To} ({move.Piece})");
                
                var notification = JsonSerializer.Serialize(new
                {
                    type = "move",
                    player = player.Name,
                    move = move
                });
                await BroadcastToWebSockets(notification);
            }
        }

        private async Task ProcessAvatarPosition(AvatarPosition position)
        {
            if (_players.ContainsKey(position.SessionId))
            {
                var player = _players[position.SessionId];
                player.AvatarX = position.X;
                player.AvatarY = position.Y;
                player.LastActivity = DateTime.Now;
                
                var notification = JsonSerializer.Serialize(new
                {
                    type = "avatar_position",
                    sessionId = position.SessionId,
                    x = position.X,
                    y = position.Y
                });
                await BroadcastToWebSockets(notification);
            }
        }

        private async Task BroadcastToWebSockets(string message)
        {
            var tasks = new List<Task>();
            
            foreach (var player in _players.Values)
            {
                if (player.WebSocket?.State == WebSocketState.Open)
                {
                    tasks.Add(SendWebSocketMessage(player.WebSocket, message));
                }
            }
            
            await Task.WhenAll(tasks);
        }

        private async Task SendWebSocketMessage(WebSocket webSocket, string message)
        {
            try
            {
                if (webSocket.State == WebSocketState.Open)
                {
                    byte[] data = Encoding.UTF8.GetBytes(message);
                    await webSocket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка отправки WebSocket сообщения: {ex.Message}");
            }
        }

        private async Task GenerateGameEventsAsync()
        {
            var events = new[]
            {
                "🎊 Турнир 'Весенний кубок' начнется через 30 минут!",
                "⭐ Найден редкий артефакт: Золотая королева!",
                "🏆 Игрок дня определен! Поздравляем чемпиона!",
                "🎁 Бонусные очки начислены всем активным игрокам!",
                "⚡ Особый режим 'Молния' доступен в течение часа!"
            };

            var random = new Random();
            
            while (_isRunning)
            {
                await Task.Delay(TimeSpan.FromMinutes(2));
                
                if (_players.Count > 0)
                {
                    var eventMessage = events[random.Next(events.Length)];
                    Console.WriteLine($"🎮 Игровое событие: {eventMessage}");
                    
                    var gameEvent = JsonSerializer.Serialize(new
                    {
                        type = "event",
                        message = eventMessage,
                        timestamp = DateTime.Now
                    });
                    
                    await BroadcastToWebSockets(gameEvent);
                }
            }
        }

        public async Task StopAsync()
        {
            _isRunning = false;
            
            _tcpListener?.Stop();
            _udpListener?.Close();
            _webSocketListener?.Stop();
            
            Console.WriteLine("🛑 Сервер остановлен");
        }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("♟️  Шахматный сервер v2.0 (с веб-поддержкой)");
            Console.WriteLine("============================================");
            
            var server = new ChessServer();
            await server.StartAsync();
        }
    }
}