// // Модуль подключения к серверу
// const Connection = {
//     // Подключение к серверу
//     async connect() {
//         const nameInput = document.getElementById('playerName');
//         const hostInput = document.getElementById('serverHost');
//         const wsPortInput = document.getElementById('wsPort');
        
//         GAME_STATE.playerName = nameInput.value.trim();
//         const host = hostInput.value.trim() || CONFIG.DEFAULT_CONNECTION.host;
//         const wsPort = wsPortInput.value.trim() || CONFIG.DEFAULT_CONNECTION.wsPort;

//         if (!GAME_STATE.playerName) {
//             Notifications.show('Введите ваше имя!', CONFIG.NOTIFICATION.types.ERROR);
//             return;
//         }

//         try {
//             // Подключение WebSocket
//             const wsUrl = `ws://${host}:${wsPort}/?sessionId=${GAME_STATE.sessionId}`;
//             GAME_STATE.webSocket = new WebSocket(wsUrl);

//             GAME_STATE.webSocket.onopen = () => {
//                 GAME_STATE.isConnected = true;
//                 GAME_STATE.connectedAt = new Date();
//                 this.updateConnectionStatus();
                
//                 // Регистрируемся на сервере
//                 this.sendMessage({
//                     type: CONFIG.MESSAGE_TYPES.REGISTER,
//                     sessionId: GAME_STATE.sessionId,
//                     playerName: GAME_STATE.playerName
//                 });

//                 Notifications.show('Подключено к серверу!', CONFIG.NOTIFICATION.types.SUCCESS);
//                 Chat.addSystemMessage(`Подключен к серверу как ${GAME_STATE.playerName}`);
//             };

//             GAME_STATE.webSocket.onmessage = (event) => {
//                 this.handleMessage(event.data);
//             };

//             GAME_STATE.webSocket.onclose = () => {
//                 GAME_STATE.isConnected = false;
//                 this.updateConnectionStatus();
//                 Notifications.show('Отключено от сервера', CONFIG.NOTIFICATION.types.ERROR);
//                 Chat.addSystemMessage('Соединение с сервером разорвано');
//             };

//             GAME_STATE.webSocket.onerror = (error) => {
//                 Notifications.show('Ошибка подключения к серверу', CONFIG.NOTIFICATION.types.ERROR);
//                 Chat.addSystemMessage('Ошибка подключения к серверу');
//                 console.error('WebSocket error:', error);
//             };

//         } catch (error) {
//             Notifications.show('Ошибка подключения: ' + error.message, CONFIG.NOTIFICATION.types.ERROR);
//             console.error('Connection error:', error);
//         }
//     },

//     // Отключение от сервера
//     disconnect() {
//         if (GAME_STATE.webSocket) {
//             GAME_STATE.webSocket.close();
//         }
//         GAME_STATE.isConnected = false;
//         GAME_STATE.connectedAt = null;
//         this.updateConnectionStatus();
//         Chat.addSystemMessage('Отключен от сервера');
//     },

//     // Обновление статуса подключения
//     updateConnectionStatus() {
//         const statusDiv = document.getElementById('connectionStatus');
//         const connectBtn = document.getElementById('connectBtn');
//         const chatInput = document.getElementById('chatInput');
//         const sendChatBtn = document.getElementById('sendChatBtn');
//         const resetBtn = document.getElementById('resetBtn');
//         const clearBtn = document.getElementById('clearBtn');

//         if (GAME_STATE.isConnected) {
//             statusDiv.innerHTML = '<span class="status-connected">✅ Подключен</span>';
//             connectBtn.textContent = '🔌 Отключиться';
//             chatInput.disabled = false;
//             sendChatBtn.disabled = false;
//             resetBtn.disabled = false;
//             clearBtn.disabled = false;
//         } else {
//             statusDiv.innerHTML = '<span class="status-disconnected">❌ Не подключен</span>';
//             connectBtn.textContent = '🔗 Подключиться';
//             chatInput.disabled = true;
//             sendChatBtn.disabled = true;
//             resetBtn.disabled = true;
//             clearBtn.disabled = true;
//         }
//     },

//     // Отправка сообщения через WebSocket
//     sendMessage(message) {
//         if (GAME_STATE.webSocket && GAME_STATE.webSocket.readyState === WebSocket.OPEN) {
//             try {
//                 GAME_STATE.webSocket.send(JSON.stringify(message));
//                 return true;
//             } catch (error) {
//                 console.error('Ошибка отправки сообщения:', error);
//                 Notifications.show('Ошибка отправки сообщения', CONFIG.NOTIFICATION.types.ERROR);
//                 return false;
//             }
//         } else {
//             Notifications.show('Соединение с сервером отсутствует', CONFIG.NOTIFICATION.types.ERROR);
//             return false;
//         }
//     },

//     // Отправка хода
//     sendMove(from, to, piece) {
//         if (!GAME_STATE.webSocket || GAME_STATE.webSocket.readyState !== WebSocket.OPEN) {
//             Notifications.show('Нет соединения с сервером', CONFIG.NOTIFICATION.types.ERROR);
//             return false;
//         }

//         const move = {
//             type: CONFIG.MESSAGE_TYPES.TCP_MOVE,
//             sessionId: GAME_STATE.sessionId,
//             from: from,
//             to: to,
//             piece: piece,
//             timestamp: new Date().toISOString()
//         };

//         if (this.sendMessage(move)) {
//             Notifications.show(`Ход отправлен: ${from} → ${to}`, CONFIG.NOTIFICATION.types.SUCCESS);
//             return true;
//         }
//         return false;
//     },

//     // Отправка сообщения в чат
//     sendChatMessage(message) {
//         if (!GAME_STATE.isConnected || !GAME_STATE.webSocket) {
//             Notifications.show('Подключитесь к серверу для отправки сообщений', CONFIG.NOTIFICATION.types.ERROR);
//             return false;
//         }

//         if (!message || message.trim().length === 0) {
//             Notifications.show('Сообщение не может быть пустым', CONFIG.NOTIFICATION.types.ERROR);
//             return false;
//         }

//         const chatMessage = {
//             type: CONFIG.MESSAGE_TYPES.CHAT,
//             sessionId: GAME_STATE.sessionId,
//             message: message.trim(),
//             timestamp: new Date().toISOString()
//         };

//         return this.sendMessage(chatMessage);
//     },

//     // Отправка позиции аватара
//     sendAvatarPosition(x, y) {
//         if (!GAME_STATE.webSocket || GAME_STATE.webSocket.readyState !== WebSocket.OPEN) {
//             return false;
//         }

//         // Валидация координат
//         if (x < 0 || x >= CONFIG.GAME.boardSize || y < 0 || y >= CONFIG.GAME.boardSize) {
//             console.warn('Неверные координаты аватара:', x, y);
//             return false;
//         }

//         const avatarPosition = {
//             type: CONFIG.MESSAGE_TYPES.AVATAR_POSITION,
//             sessionId: GAME_STATE.sessionId,
//             x: x,
//             y: y,
//             timestamp: new Date().toISOString()
//         };

//         return this.sendMessage(avatarPosition);
//     },

//     // Обработка входящих сообщений
//     handleMessage(data) {
//         try {
//             const message = JSON.parse(data);
            
//             switch (message.type) {
//                 case CONFIG.MESSAGE_TYPES.MOVE:
//                     if (message.player && message.move) {
//                         ChessBoard.handleRemoteMove(message.player, message.move);
//                     }
//                     break;

//                 case CONFIG.MESSAGE_TYPES.CHAT:
//                     if (message.player && message.message) {
//                         Chat.addMessage(message.player, message.message, 'user');
//                     }
//                     break;

//                 case CONFIG.MESSAGE_TYPES.EVENT:
//                     if (message.message) {
//                         Chat.addMessage('Система', message.message, 'event');
//                         GAME_STATE.eventHistory.push({
//                             message: message.message,
//                             timestamp: new Date()
//                         });
//                     }
//                     break;

//                 case CONFIG.MESSAGE_TYPES.AVATAR_POSITION:
//                     if (message.sessionId && message.x !== undefined && message.y !== undefined) {
//                         ChessBoard.updatePlayerAvatar(message.sessionId, message.x, message.y);
//                     }
//                     break;

//                 case 'player_joined':
//                     if (message.playerName) {
//                         Chat.addSystemMessage(`Игрок ${message.playerName} присоединился к игре`);
//                         Notifications.show(`${message.playerName} присоединился`, CONFIG.NOTIFICATION.types.INFO);
//                     }
//                     break;

//                 case 'player_left':
//                     if (message.playerName) {
//                         Chat.addSystemMessage(`Игрок ${message.playerName} покинул игру`);
//                         Notifications.show(`${message.playerName} ушел`, CONFIG.NOTIFICATION.types.INFO);
//                     }
//                     break;

//                 default:
//                     console.log('Неизвестный тип сообщения:', message.type, message);
//             }
//         } catch (error) {
//             // Если не JSON, то простое текстовое сообщение
//             console.log('Получено текстовое сообщение:', data);
//             Chat.addMessage('Сервер', data, 'system');
//         }
//     },

//     // Проверка состояния соединения
//     getConnectionStatus() {
//         return {
//             isConnected: GAME_STATE.isConnected,
//             connectedAt: GAME_STATE.connectedAt,
//             sessionId: GAME_STATE.sessionId,
//             playerName: GAME_STATE.playerName,
//             webSocketState: GAME_STATE.webSocket ? GAME_STATE.webSocket.readyState : null
//         };
//     },

//     // Переподключение при разрыве связи
//     async reconnect() {
//         if (GAME_STATE.isConnected) {
//             console.log('Уже подключен к серверу');
//             return;
//         }

//         console.log('Попытка переподключения...');
//         Chat.addSystemMessage('Попытка переподключения к серверу...');
        
//         await this.connect();
//     },

//     // Проверка соединения (heartbeat)
//     startHeartbeat() {
//         if (this.heartbeatInterval) {
//             clearInterval(this.heartbeatInterval);
//         }

//         this.heartbeatInterval = setInterval(() => {
//             if (GAME_STATE.isConnected && GAME_STATE.webSocket) {
//                 if (GAME_STATE.webSocket.readyState === WebSocket.OPEN) {
//                     this.sendMessage({
//                         type: 'ping',
//                         sessionId: GAME_STATE.sessionId,
//                         timestamp: new Date().toISOString()
//                     });
//                 } else {
//                     console.log('Соединение разорвано, попытка переподключения...');
//                     this.reconnect();
//                 }
//             }
//         }, 30000); // Проверка каждые 30 секунд
//     },

//     // Остановка heartbeat
//     stopHeartbeat() {
//         if (this.heartbeatInterval) {
//             clearInterval(this.heartbeatInterval);
//             this.heartbeatInterval = null;
//         }
//     }
// };

// // Глобальная функция для совместимости с HTML
// async function toggleConnection() {
//     if (GAME_STATE.isConnected) {
//         Connection.disconnect();
//         Connection.stopHeartbeat();
//     } else {
//         await Connection.connect();
//         Connection.startHeartbeat();
//     }
// }