FrozenDynamic
=============

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  ДЛЯ ПОЛЬЗОВАТЕЛЕЙ — папка "Ready To Use"
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Требования:
  • Script Hook V            — http://www.dev-c.com/gtav/scripthookv/
  • Script Hook V .NET 3     — https://github.com/scripthookvdotnet/scripthookvdotnet-nightly/releases
  • LemonUI.SHVDN3.dll       — https://gta5-mods.com/tools/lemonui

Установка:
  1. Установите Script Hook V и Script Hook V .NET в корень GTA V
  2. Скачайте LemonUI и скопируйте LemonUI.SHVDN3.dll в GTA V\scripts\
  3. Скопируйте FrozenDynamic.dll в GTA V\scripts\

В GTA V\scripts\ должно быть:
  • FrozenDynamic.dll
  • LemonUI.SHVDN3.dll

==========================
  ИСПОЛЬЗОВАНИЕ
==========================

Горячие клавиши:
  • K — открыть/закрыть главное меню
  • Backspace — навигация назад в подменю

Главное меню (Frozen Dynamic):
  • Заморозить NPC — остановить всех пешеходов на месте
    (сохраняет танцевальные анимации и сценарии)
  • Разморозить NPC — вернуть NPC к обычному поведению
    (педы стоят с полным игнором игрока, танцы перезапускаются)

Особенности:
  • Заморозка сохраняет текущие анимации NPC (танцы, празднования)
  • Размороженные NPC стоят на месте с полным игнором игрока
  • Tick-based maintenance поддерживает состояние заморозки
  • Проверка на уже замороженных/размороженных NPC
  • Уведомления о количестве обработанных NPC

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
  ДЛЯ РАЗРАБОТЧИКОВ — папка "Source Code"
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Исходный код проекта, решение Visual Studio и зависимости.
Сборка через FrozenDynamic.sln в Visual Studio (x64, .NET Framework 4.8).

Или через командную строку: dotnet build -c Release

Архитектура мода:
  • При freeze: педы замораживаются, анимации сохраняются (dict+anim+progress)
  • При unfreeze: педы стоят с полным игнором игрока, анимации перезапускаются
  • Tick-based maintenance поддерживает состояние "игнорирования"
