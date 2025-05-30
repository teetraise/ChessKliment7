// Конфигурация приложения
const CONFIG = {
    // Начальная расстановка фигур
    INITIAL_BOARD: {
        'a8': '♜', 'b8': '♞', 'c8': '♝', 'd8': '♛', 'e8': '♚', 'f8': '♝', 'g8': '♞', 'h8': '♜',
        'a7': '♟', 'b7': '♟', 'c7': '♟', 'd7': '♟', 'e7': '♟', 'f7': '♟', 'g7': '♟', 'h7': '♟',
        'a2': '♙', 'b2': '♙', 'c2': '♙', 'd2': '♙', 'e2': '♙', 'f2': '♙', 'g2': '♙', 'h2': '♙',
        'a1': '♖', 'b1': '♘', 'c1': '♗', 'd1': '♕', 'e1': '♔', 'f1': '♗', 'g1': '♘', 'h1': '♖'
    },

    // Настройки подключения по умолчанию
    DEFAULT_CONNECTION: {
        host: 'localhost',
        tcpPort: 8080,
        udpPort: 8081,
        wsPort: 8082
    },

    // Настройки уведомлений
    NOTIFICATION: {
        duration: 3000, // 3 секунды
        types: {
            SUCCESS: 'success',
            ERROR: 'error',
            INFO: 'info'
        }
    },

    // Настройки игры
    GAME: {
        boardSize: 8,
        squareSize: 60, // пикселей
        maxHistoryItems: 50
    },

    // Типы сообщений WebSocket
    MESSAGE_TYPES: {
        REGISTER: 'register',
        TCP_MOVE: 'tcp_move',
        CHAT: 'chat',
        AVATAR_POSITION: 'avatar_position',
        MOVE: 'move',
        EVENT: 'event'
    }
};

// Глобальные переменные состояния
const GAME_STATE = {
    isConnected: false,
    sessionId: '',
    playerName: '',
    webSocket: null,
    selectedSquare: null,
    playerAvatars: new Map(),
    gameBoard: {},
    moveHistory: [],
    eventHistory: []
};

// Утилитарные функции
const Utils = {
    // Генерация уникального ID сессии
    generateSessionId() {
        return Math.random().toString(36).substring(2, 10);
    },

    // Получение координат поля по ID
    getSquareCoords(squareId) {
        const file = squareId.charCodeAt(0) - 97; // a=0, b=1, etc.
        const rank = parseInt(squareId[1]) - 1; // 1=0, 2=1, etc.
        return { file, rank };
    },

    // Получение ID поля по координатам
    getSquareId(file, rank) {
        const fileChar = String.fromCharCode(97 + file);
        return fileChar + (rank + 1);
    },

    // Проверка валидности поля
    isValidSquare(squareId) {
        return /^[a-h][1-8]$/.test(squareId);
    },

    // Форматирование времени
    formatTime(date) {
        return date.toLocaleTimeString();
    },

    // Форматирование даты и времени
    formatDateTime(date) {
        return date.toLocaleString();
    }
};