# Connect Four - ASP.NET Core Web Application

Wieloosobowa gra turowa "Cztery w rzêdzie" (Connect Four) implementowana w ASP.NET Core 6.0 z real-time komunikacj¹ przez SignalR.

## ?? Cechy aplikacji

- **Real-time Multiplayer**: Dwaj gracze graj¹ w tym samym pokoju dziêki WebSocket (SignalR)
- **Zarz¹dzanie pokojami**: Tworzenie i do³¹czanie do pokoi z limitami aktywnych sesji
- **Ranking graczy**: Automaticznie aktualizowany ranking top 10 graczy
- **Historia partii**: Zapis ostatnich 20 zakoñczonych gier
- **Asynchroniczna architektura**: Pe³ne wsparcie async/await
- **Wielow¹tkowe przetwarzanie**: Semafor, Bariera, Lock oraz Thread Pool

## ?? Stos Techniczny

| Warstwa | Technologia |
|---|---|
| **Runtime** | .NET 6.0 |
| **Backend** | ASP.NET Core MVC |
| **ORM** | Entity Framework Core 6 |
| **Baza danych** | SQLite |
| **Real-time** | SignalR (WebSocket) |
| **Frontend** | Razor + vanilla JavaScript + CSS3 |

## ?? Wymagane pakiety NuGet

```xml
<ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="6.0.0" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="6.0.0" />
    <PackageReference Include="Microsoft.AspNetCore.SignalR" Version="1.1.0" />
</ItemGroup>
```

## ?? Uruchamianie aplikacji

### Wymagania wstêpne
- .NET 6.0 SDK zainstalowane
- Visual Studio 2022 lub Visual Studio Code

### Kroki uruchamiania

1. **Klonowanie repozytorium**
```bash
git clone https://github.com/piotrgendaszewski/connect_four
cd connect_four
```

2. **Przywrócenie pakietów**
```bash
dotnet restore
```

3. **Migracja bazy danych**
Baza bêdzie automatycznie zmigrowana przy pierwszym uruchomieniu.

4. **Uruchamianie aplikacji**
```bash
dotnet run
```

Aplikacja bêdzie dostêpna pod adresem `https://localhost:5001` lub `http://localhost:5000`

## ?? Struktura Projektu

```
ConnectFour/
??? Controllers/
?   ??? HomeController.cs      # Obs³uga lobby, ranking, historia
?   ??? GameController.cs      # Tworzenie/do³¹czanie do pokoi
??? Hubs/
?   ??? GameHub.cs             # SignalR Hub do komunikacji real-time
??? Models/
?   ??? Player.cs              # Model gracza
?   ??? GameRecord.cs          # Model historii gry
?   ??? GameBoard.cs           # Reprezentacja planszy
?   ??? GameRoom.cs            # Stan pokoju (w pamiêci)
?   ??? RequestDtos.cs         # DTO dla requestów API
??? Services/
?   ??? GameEngine.cs          # Logika gry (sprawdzanie wygranych)
?   ??? RoomManager.cs         # Zarz¹dzanie pokojami + synchronizacja
?   ??? MoveResult.cs          # Wynik ruchu
?   ??? BackgroundTaskService.cs # Zadania w tle (cleanup, cache, ping)
??? Data/
?   ??? AppDbContext.cs        # Entity Framework Context
??? Migrations/                # EF Core migracje
??? Views/
?   ??? Home/
?   ?   ??? Index.cshtml       # Lobby - lista pokoi
?   ?   ??? Ranking.cshtml     # Ranking graczy
?   ?   ??? History.cshtml     # Historia partii
?   ??? Game/
?       ??? Play.cshtml        # Plansza gry
??? wwwroot/
?   ??? css/
?   ?   ??? game.css
?   ??? js/
?       ??? lobby.js           # AJAX polling pokoi
?       ??? game.js            # Logika gry na froncie
??? Program.cs                 # Konfiguracja DI i middleware
??? appsettings.json          # Konfiguracja aplikacji
??? ConnectFour.csproj        # Plik projektu
```

## ?? Konfiguracja (appsettings.json)

```json
{
  "Game": {
    "MaxActiveRooms": 10,                            // Max pokoi
    "PlayerReadyTimeoutSeconds": 60,                 // Timeout barier
    "InactiveRoomCleanupIntervalSeconds": 60,        // Czyszczenie
    "RankingCacheRefreshSeconds": 30,                // Odœwie¿anie rankingu
    "WebSocketPingIntervalSeconds": 10               // Ping WebSocket
  },
  "ConnectionStrings": {
    "Default": "Data Source=connectfour.db"          // Œcie¿ka SQLite
  }
}
```

## ?? Jak graæ

1. **Utwórz pokój**: Wpisz swój nick (3-20 znaków) i kliknij "Utwórz pokój"
2. **Do³¹cz do pokoju**: Drugi gracz do³¹cza, wpisuj¹c swój nick
3. **Rozpocznij grê**: Obaj gracze klikaj¹ "Gotowy!" aby zsynchronizowaæ start
4. **Graj**: Kliknij na kolumnê, aby wrzuciæ pionek
5. **Wygraj**: Ustawiaj 4 pionki w rzêdzie (poziomo, pionowo, ukoœnie)

## ?? Mechanizmy Synchronizacji

### Lock (Mutex)
Chroni dostêp do planszy w `RoomManager.MakeMove()`:
```csharp
lock(room.BoardLock) { /* operacje na planszy */ }
```

### Semafor
Ogranicza liczbê aktywnych pokoi w `RoomManager.CreateRoom()`:
```csharp
SemaphoreSlim _roomSemaphore = new(maxRooms);
bool acquired = await _roomSemaphore.WaitAsync(TimeSpan.FromSeconds(5));
```

### Bariera
Synchronizuje gotowoœæ obu graczy w `RoomManager.SignalReady()`:
```csharp
Barrier barrier = new(2);
bool success = barrier.SignalAndWait(TimeSpan.FromSeconds(60));
```

### Thread Pool
Asynchroniczne zadania w tle:
```csharp
ThreadPool.QueueUserWorkItem(_ => { /* cleanup */ });
Task.Run(async () => { /* cache refresh */ });
```

## ?? Baza Danych

### Tabela `Players`
- `Id` (int, Primary Key)
- `Nick` (string, Unique)
- `Wins` (int)
- `Losses` (int)
- `Draws` (int)

### Tabela `GameRecords`
- `Id` (int, Primary Key)
- `Player1Nick` (string)
- `Player2Nick` (string)
- `WinnerNick` (string, nullable)
- `DurationSeconds` (int)
- `PlayedAt` (DateTime)

## ?? SignalR Events

### Serwer ? Klient
- `PlayerJoined`: Gracz do³¹czy³ do pokoju
- `GameStart`: Gra siê rozpoczê³a
- `MoveResult`: Wynik ruchu innego gracza
- `GameOver`: Koniec gry (wygrana/remis)
- `PlayerLeft`: Gracz opuœci³ grê
- `MoveError`: B³¹d podczas ruchu
- `Ping`: Heartbeat (co 10 sekund)

### Klient ? Serwer
- `JoinRoom(roomId, nick)`: Do³¹cz do pokoju
- `SignalReady(roomId, nick)`: Sygnalizuj gotowoœæ
- `MakeMove(roomId, column, playerNumber)`: Wykonaj ruch

## ?? Logika Gry

Plansze 6×7 (rzêdy × kolumny):
- `0` = puste
- `1` = Gracz 1 (¿ó³ty)
- `2` = Gracz 2 (czerwony)

Sprawdzanie wygranych kierunkami:
- Poziomo (??)
- Pionowo (??)
- Ukoœnie (? i ?)

## ?? Cykl ¿ycia pokoju

```
Waiting ? Ready ? InProgress ? Finished
```

Stanym mo¿na przejœæ:
- `Waiting ? Ready` (do³¹czy³ 2. gracz)
- `Ready ? InProgress` (obaj sygnalizowali Ready)
- `InProgress ? Finished` (wygrana/remis/disconnect)
- `Waiting/Ready ? Finished` (timeout/cleanup)

## ?? Logowanie

Wszystkie operacje s¹ logowane:
```csharp
_logger.LogInformation("Room {RoomId} created", roomId);
_logger.LogWarning("Invalid move in room {RoomId}", roomId);
_logger.LogError(ex, "Error during game");
```

Konfiguracja w `appsettings.json`:
```json
"Logging": {
  "LogLevel": {
    "Default": "Information",
    "Microsoft.AspNetCore": "Warning"
  }
}
```

## ?? Wydajnoœæ

- **AJAX Polling**: Co 3 sekundy pobiera listê pokoi
- **Cache Ranking**: Odœwie¿any co 30 sekund
- **WebSocket Ping**: Co 10 sekund aby utrzymaæ po³¹czenie
- **Cleanup**: Co 60 sekund usuwa nieaktywne pokoje

## ?? API Endpoints

### GET endpoints
- `GET /` ? Lobby (Index.cshtml)
- `GET /Ranking` ? Ranking graczy
- `GET /History` ? Historia partii
- `GET /api/rooms` ? JSON lista pokoi
- `GET /api/ranking` ? JSON top 10 graczy
- `GET /api/history` ? JSON ostatnie 20 partii
- `GET /Game/Play/{roomId}` ? Strona gry

### POST endpoints
- `POST /Game/Create` ? Utwórz pokój (JSON: `{nick}`)
- `POST /Game/Join` ? Do³¹cz do pokoju (JSON: `{roomId, nick}`)

## ?? Debugowanie

Aby w³¹czyæ szczegó³owe logowanie:

```json
"Logging": {
  "LogLevel": {
    "Default": "Debug",
    "Microsoft": "Debug"
  }
}
```

## ?? Responsywnoœæ

Aplikacja jest responsywna na urz¹dzeniach:
- Desktop (1200px+)
- Tablet (768px - 1199px)
- Mobile (< 768px)

## ? Performance Tips

1. **Unikaj równoczesnych ruchów**: Lock na planszy zapewnia thread-safety
2. **Limit pokoi**: Semafor zapobiega przeci¹¿eniu
3. **Cache ranking**: Baza jest aktualizowana asynchronicznie
4. **WebSocket zamiast HTTP polling**: SignalR dla real-time

## ?? Licencja

MIT License - Zobacz LICENSE file

## ????? Autor

Piotr Gendaszewski
- GitHub: https://github.com/piotrgendaszewski
- Repozytorium: https://github.com/piotrgendaszewski/connect_four

---

**Wersja**: 1.0.0  
**Data**: 28.06.2026  
**Status**: ? Kompletna implementacja zaliczeniowa
