// // Модуль шахматной доски
// const ChessBoard = {
//     // Создание шахматной доски
//     create() {
//         const board = document.getElementById('chessBoard');
//         board.innerHTML = '';

//         for (let row = 8; row >= 1; row--) {
//             for (let col = 0; col < 8; col++) {
//                 const square = document.createElement('div');
//                 const file = String.fromCharCode(97 + col); // a-h
//                 const squareId = file + row;
                
//                 square.className = `chess-square ${(row + col) % 2 === 0 ? 'black' : 'white'}`;
//                 square.id = squareId;
//                 square.onclick = () => this.handleSquareClick(squareId);
                
//                 board.appendChild(square);
//             }
//         }
//     },

//     // Установка начальной позиции
//     setupInitialPosition() {
//         // Очищаем доску
//         document.querySelectorAll('.chess-square').forEach(square => {
//             square.textContent = '';
//             square.classList.remove('selected', 'possible-move');
//         });

//         // Расставляем фигуры
//         for (const [square, piece] of Object.entries(CONFIG.INITIAL_BOARD)) {
//             const element = document.getElementById(square);
//             if (element) {
//                 element.textContent = piece;
//             }
//         }

//         GAME_STATE.gameBoard = { ...CONFIG.INITIAL_BOARD };
//     },

//     // Обработка клика по полю
//     handleSquareClick(squareId) {
//         if (!GAME_STATE.isConnected) {
//             Notifications.show('Подключитесь к серверу для игры!', CONFIG.NOTIFICATION.types.ERROR);
//             return;
//         }

//         const square = document.getElementById(squareId);
        
//         if (GAME_STATE.selectedSquare === null) {
//             // Выбор фигуры
//             if (square.textContent && square.textContent.trim() !== '') {
//                 GAME_STATE.selectedSquare = squareId;
//                 square.classList.add('selected');
//                 this.highlightPossibleMoves(squareId);
//             }
//         } else {
//             // Выполнение хода
//             if (GAME_STATE.selectedSquare === squareId) {
//                 // Отмена выбора
//                 this.clearSelection();
//             } else {
//                 // Выполнение хода
//                 this.makeMove(GAME_STATE.selectedSquare, squareId);
//             }
//         }
//     },

//     // Подсветка возможных ходов
//     highlightPossibleMoves(fromSquare) {
//         const piece = GAME_STATE.gameBoard[fromSquare];
//         if (!piece) return;

//         // Простая подсветка (можно расширить правильной логикой)
//         const allSquares = Object.keys(GAME_STATE.gameBoard);
//         const possibleMoves = allSquares.filter(sq => !GAME_STATE.gameBoard[sq]).slice(0, 3);
        
//         possibleMoves.forEach(square => {
//             const element = document.getElementById(square);
//             if (element) {
//                 element.classList.add('possible-move');
//             }
//         });
//     },

//     // Очистка выделения
//     clearSelection() {
//         document.querySelectorAll('.chess-square').forEach(square => {
//             square.classList.remove('selected', 'possible-move');
//         });
//         GAME_STATE.selectedSquare = null;
//     },

//     // Выполнение хода
//     makeMove(from, to) {
//         const piece = GAME_STATE.gameBoard[from];
//         if (!piece) return;

//         // Обновляем локальное состояние
//         GAME_STATE.gameBoard[to] = piece;
//         delete GAME_STATE.gameBoard[from];

//         // Обновляем визуализацию
//         document.getElementById(from).textContent = '';
//         document.getElementById(to).textContent = piece;

//         // Отправляем ход
//         Connection.sendMove(from, to, piece);
        
//         this.clearSelection();
        
//         // Добавляем в историю ходов
//         GameLogic.addMoveToHistory(GAME_STATE.playerName, from, to, piece);
//     },

//     // Обработка хода от другого игрока
//     handleRemoteMove(player, move) {
//         if (move.sessionId === GAME_STATE.sessionId) return; // Не обрабатываем свои ходы

//         // Обновляем доску
//         GAME_STATE.gameBoard[move.to] = GAME_STATE.gameBoard[move.from] || '♟';
//         delete GAME_STATE.gameBoard[move.from];

//         document.getElementById(move.from).textContent = '';
//         document.getElementById(move.to).textContent = GAME_STATE.gameBoard[move.to];

//         // Добавляем в историю
//         GameLogic.addMoveToHistory(player, move.from, move.to, move.piece);
        
//         Notifications.show(`${player} сделал ход: ${move.from} → ${move.to}`, CONFIG.NOTIFICATION.types.INFO);
//     },

//     // Обновление позиции аватара
//     updatePlayerAvatar(playerId, x, y) {
//         if (playerId === GAME_STATE.sessionId) return; // Не показываем свой аватар

//         // Удаляем предыдущую позицию аватара этого игрока
//         document.querySelectorAll('.avatar-indicator').forEach(indicator => {
//             if (indicator.dataset.playerId === playerId) {
//                 indicator.remove();
//             }
//         });

//         // Добавляем аватар на новую позицию
//         const file = String.fromCharCode(97 + Math.floor(x));
//         const rank = Math.floor(y) + 1;
//         const squareId = file + rank;
//         const square = document.getElementById(squareId);
        
//         if (square) {
//             const indicator = document.createElement('div');
//             indicator.className = 'avatar-indicator';
//             indicator.dataset.playerId = playerId;
//             square.appendChild(indicator);
//         }
//     },

//     // Сброс доски
//     reset() {
//         this.setupInitialPosition();
//         this.clearSelection();
//         Notifications.show('Доска сброшена', CONFIG.NOTIFICATION.types.INFO);
//     }
// };

// // Глобальные функции для совместимости с HTML
// function resetBoard() {
//     ChessBoard.reset();
// }

// function clearSelection() {
//     ChessBoard.clearSelection();
// }