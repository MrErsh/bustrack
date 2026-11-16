# BusTrack

Android-приложение (.NET MAUI) для отслеживания автобусов Санкт-Петербурга в реальном времени.

Источник данных: открытый GTFS Realtime API портала общественного транспорта СПб
(transport.orgp.spb.ru), без ключей и токенов.

## Возможности

- выбор маршрута на экране настроек (каталог всех автобусных маршрутов встроен в приложение);
- запуск/остановка трекинга отдельной кнопкой;
- список автобусов на линии: ближайшая остановка, направление, скорость, свежесть данных;
- фоновый трекинг (Android foreground service) и локальные push-уведомления
  о приближении автобуса к выбранной остановке.

## Структура

- `src/BusTrack.Model` — доменные модели;
- `src/BusTrack.Api` — клиент GTFS Realtime (protobuf) + встроенный каталог маршрутов/остановок;
- `src/BusTrack.Core` — движок трекинга, привязка к остановкам, логика уведомлений (без UI);
- `src/BusTrack.App` — MAUI-приложение (Android);
- `tests/BusTrack.Tests` — xUnit + Moq + FluentAssertions.

## Сборка

Требуется .NET SDK 11 (см. `global.json`) и workload `android`.

```
dotnet build
dotnet test
```

APK: `dotnet publish src/BusTrack.App -f net11.0-android -c Release`.