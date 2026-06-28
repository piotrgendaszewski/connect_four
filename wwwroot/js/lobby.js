// AJAX polling for rooms list
let pollInterval;
const lastNickStorageKey = 'connectFourLastNick';

function initLobby() {
    const createNickInput = document.getElementById('createNick');
    createNickInput?.addEventListener('focus', () => autofillNickIfEmpty(createNickInput));
    createNickInput?.addEventListener('click', () => autofillNickIfEmpty(createNickInput));
    createNickInput?.addEventListener('keydown', (e) => {
        if (e.key === 'Enter') {
            e.preventDefault();
            createRoom();
        }
    });

    loadRooms();
    pollInterval = setInterval(loadRooms, 3000);
}

async function loadRooms() {
    try {
        const response = await fetch('/api/rooms');
        if (!response.ok) throw new Error('Failed to load rooms');

        const rooms = await response.json();
        renderRoomsTable(rooms);
    } catch (error) {
        console.error('Error loading rooms:', error);
        updateRoomsInfo(`Błąd: ${error.message}`);
    }
}

function renderRoomsTable(rooms) {
    const tbody = document.getElementById('roomsBody');
    const activeElement = document.activeElement;
    const focusedRoomId = activeElement?.classList?.contains('join-nick-input')
        ? activeElement.dataset.roomId
        : null;
    const selectionStart = focusedRoomId ? activeElement.selectionStart : null;
    const selectionEnd = focusedRoomId ? activeElement.selectionEnd : null;

    if (rooms.length === 0) {
        tbody.innerHTML = '<tr><td colspan="6" style="text-align: center; padding: 30px;">Brak dostępnych pokoi</td></tr>';
        updateRoomsInfo('Brak pokoi - utwórz nowy!');
        return;
    }

    updateRoomsInfo(`Dostępne pokoje: ${rooms.length}`);

    tbody.innerHTML = rooms.map(room => `
        <tr>
            <td><code>${room.roomId.substring(0, 8)}...</code></td>
            <td>${escapeHtml(room.player1Nick)}</td>
            <td>${room.player2Nick ? escapeHtml(room.player2Nick) : '-'}</td>
            <td>
                <span class="room-status ${room.status === 'Waiting' ? 'status-waiting' : 'status-ready'}">
                    ${room.status === 'Waiting' ? '? Oczekuje' : '? Gotowy'}
                </span>
            </td>
            <td>${new Date(room.createdAt).toLocaleTimeString('pl-PL')}</td>
            <td>
                ${room.status === 'Waiting' ? `
                    <div class="join-form" style="display: flex; gap: 5px;">
                        <input type="text" class="join-nick-input" data-room-id="${room.roomId}" placeholder="Nick" maxlength="20" value="${escapeHtml(getJoinNickDraft(room.roomId))}">
                        <button onclick="joinRoom('${room.roomId}', this)">Dołącz</button>
                    </div>
                ` : '<span style="color: #999;">Pełny</span>'}
            </td>
        </tr>
    `).join('');

    document.querySelectorAll('.join-nick-input').forEach(input => {
        input.addEventListener('input', () => saveJoinNickDraft(input.dataset.roomId, input.value));
        input.addEventListener('focus', () => autofillNickIfEmpty(input));
        input.addEventListener('click', () => autofillNickIfEmpty(input));
        input.addEventListener('keydown', (e) => {
            if (e.key === 'Enter') {
                e.preventDefault();
                const roomId = input.dataset.roomId;
                if (!roomId) {
                    return;
                }

                autofillNickIfEmpty(input);
                const nick = input.value.trim();
                if (!nick) {
                    showError('Wpisz swój nick');
                    return;
                }

                if (!/^[a-zA-Z0-9]{3,20}$/.test(nick)) {
                    showError('Nick musi mieć 3-20 znaków i zawierać tylko litery i cyfry');
                    return;
                }

                saveJoinNickDraft(roomId, nick);
                saveLastNick(nick);
                joinRoomWithNick(roomId, nick);
            }
        });
    });

    if (focusedRoomId) {
        const inputToRefocus = document.querySelector(`.join-nick-input[data-room-id="${focusedRoomId}"]`);
        if (inputToRefocus) {
            inputToRefocus.focus();
            if (selectionStart !== null && selectionEnd !== null) {
                inputToRefocus.setSelectionRange(selectionStart, selectionEnd);
            }
        }
    }
}

function updateRoomsInfo(text) {
    document.getElementById('roomsCount').textContent = text;
}

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

function getJoinNickDraft(roomId) {
    return localStorage.getItem(`connectFourJoinNick_${roomId}`) || '';
}

function saveJoinNickDraft(roomId, nick) {
    localStorage.setItem(`connectFourJoinNick_${roomId}`, nick);
}

function clearJoinNickDraft(roomId) {
    localStorage.removeItem(`connectFourJoinNick_${roomId}`);
}

function getLastNick() {
    return localStorage.getItem(lastNickStorageKey) || '';
}

function saveLastNick(nick) {
    localStorage.setItem(lastNickStorageKey, nick);
}

function autofillNickIfEmpty(input) {
    if (!input) {
        return;
    }

    const currentValue = input.value.trim();
    if (currentValue) {
        return;
    }

    const lastNick = getLastNick();
    if (lastNick) {
        input.value = lastNick;
    }
}

function showError(message) {
    const errorDiv = document.getElementById('errorMessage');
    errorDiv.textContent = message;
    errorDiv.classList.add('show');
    setTimeout(() => {
        errorDiv.classList.remove('show');
    }, 5000);
}

async function createRoom() {
    const createNickInput = document.getElementById('createNick');
    autofillNickIfEmpty(createNickInput);
    const nick = createNickInput.value.trim();

    if (!nick) {
        showError('Wpisz swój nick');
        return;
    }

    if (!/^[a-zA-Z0-9]{3,20}$/.test(nick)) {
        showError('Nick musi mieć 3-20 znaków i zawierać tylko litery i cyfry');
        return;
    }

    saveLastNick(nick);

    try {
        const response = await fetch('/Game/Create', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ nick })
        });

        if (response.status === 503) {
            showError('Brak wolnych pokoi. Spróbuj ponownie za chwilę.');
            return;
        }

        if (!response.ok) {
            showError('Błąd podczas tworzenia pokoju');
            return;
        }

        const data = await response.json();
        document.getElementById('createNick').value = '';

        // Redirect to game room as player 1
        window.location.href = `/Game/Play/${data.roomId}?nick=${encodeURIComponent(nick)}&player=1`;
    } catch (error) {
        console.error('Error creating room:', error);
        showError('Błąd sieci: ' + error.message);
    }
}

function joinRoom(roomId, button) {
    const row = button.closest('tr');
    const nickInput = row.querySelector('.join-nick-input');
    autofillNickIfEmpty(nickInput);
    const nick = nickInput.value.trim();

    if (!nick) {
        showError('Wpisz swój nick');
        return;
    }

    if (!/^[a-zA-Z0-9]{3,20}$/.test(nick)) {
        showError('Nick musi mieć 3-20 znaków i zawierać tylko litery i cyfry');
        return;
    }

    saveJoinNickDraft(roomId, nick);
    saveLastNick(nick);
    joinRoomWithNick(roomId, nick);
}

async function joinRoomWithNick(roomId, nick) {
    try {
        const response = await fetch('/Game/Join', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ roomId, nick })
        });

        if (!response.ok) {
            showError('Nie można dołączyć do pokoju');
            return;
        }

        clearJoinNickDraft(roomId);
        const data = await response.json();
        window.location.href = `/Game/Play/${data.roomId}?nick=${encodeURIComponent(nick)}&player=2`;
    } catch (error) {
        console.error('Error joining room:', error);
        showError('Błąd sieci: ' + error.message);
    }
}

// Initialize on page load
window.addEventListener('load', initLobby);
window.addEventListener('beforeunload', () => {
    clearInterval(pollInterval);
});
