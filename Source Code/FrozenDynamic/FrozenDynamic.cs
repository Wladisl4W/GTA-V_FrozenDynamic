using GTA;
using GTA.Native;
using LemonUI;
using LemonUI.Menus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace FrozenDynamic
{
    /// <summary>
    /// Мод для заморозки/разморозки всех NPC в GTA V
    /// Управление: K - открыть меню
    ///
    /// Архитектура:
    /// - При freeze: педы замораживаются, танцевальные анимации сохраняются (dict+anim+progress)
    /// - При unfreeze: педы стоят с полным игнором игрока, танцы перезапускаются
    /// - Tick-based maintenance поддерживает состояние "игнорирования"
    /// </summary>
    public class FrozenDynamicMod : Script
    {
        private readonly NativeMenu _menu;
        private readonly NativeItem _freezeItem;
        private readonly NativeItem _unfreezeItem;
        private bool _isFrozen = false;

        // Трекинг замороженных педов
        private readonly HashSet<int> _frozenPeds = new HashSet<int>();

        // Сохранённые анимации педов (только танцы/сценарии которые мы можем определить)
        private readonly Dictionary<int, PedAnimState> _pedAnimStates = new Dictionary<int, PedAnimState>();

        // Tick counter для maintenance interval
        private int _tickCounter = 0;
        private const int MAINTAIN_INTERVAL = 30; // ~500ms при 60fps — только для проверки мертвых педов

        // Конфигурация
        private const Keys MenuToggleKey = Keys.K;

        /// <summary>
        /// Список известных танцевальных анимаций для определения
        /// </summary>
        private static readonly (string dict, string anim)[] DanceAnims = new[]
        {
            // MP Celebration анимации (мужские)
            ("anim@mp_player_intcelebrationmale", "idle_a"),
            ("anim@mp_player_intcelebrationmale", "idle_b"),
            ("anim@mp_player_intcelebrationmale", "idle_c"),
            ("anim@mp_player_intcelebrationmale", "idle_d"),
            ("anim@mp_player_intcelebrationmale", "idle_e"),
            ("anim@mp_player_intcelebrationmale", "idle_f"),
            // MP Celebration анимации (женские)
            ("anim@mp_player_intcelebrationfemale", "idle_a"),
            ("anim@mp_player_intcelebrationfemale", "idle_b"),
            ("anim@mp_player_intcelebrationfemale", "idle_c"),
            ("anim@mp_player_intcelebrationfemale", "idle_d"),
            ("anim@mp_player_intcelebrationfemale", "idle_e"),
            ("anim@mp_player_intcelebrationfemale", "idle_f"),
            // Clown / Gumbo Dance
            ("move_clown@p_m_zero_idles@", "fidget_short_dance"),
            ("move_clown@p_m_one_idles@", "fidget_short_dance"),
            ("move_clown@p_m_zero_idles@", "fidget_dance_enter"),
            ("move_clown@p_m_one_idles@", "fidget_dance_enter"),
            // World Human Dancing
            ("amb@world_human_dancing@male@base", "base"),
            ("amb@world_human_dancing@female@base", "base"),
            // Strip Club Pole Dance
            ("mini@strip_club@pole_dance@pole_a_2_stage", "pd_a2_stage"),
            ("mini@strip_club@pole_dance@pole_a_1_stage", "pd_a1_stage"),
            ("mini@strip_club@pole_dance@pole_b_2_stage", "pd_b2_stage"),
            ("mini@strip_club@pole_dance@pole_b_1_stage", "pd_b1_stage"),
            // Dancing @ Club
            ("anim@amb@nightclub@mini@dance@dance_01@dance@", "solo"),
            ("anim@amb@nightclub@mini@dance@dance_02@dance@", "solo"),
            ("anim@amb@nightclub@mini@dance@dance_03@dance@", "solo"),
            ("anim@amb@nightclub@mini@dance@dance_04@dance@", "solo"),
            ("anim@amb@nightclub@mini@dance@dance_05@dance@", "solo"),
            ("anim@amb@nightclub@mini@dance@dance_06@dance@", "solo"),
            // Club dance couple
            ("anim@amb@nightclub@mini@dance@dance_paired@dance_01@", "couple_dance"),
            ("anim@amb@nightclub@mini@dance@dance_paired@dance_02@", "couple_dance"),
            ("anim@amb@nightclub@mini@dance@dance_paired@dance_03@", "couple_dance"),
            // Hands up dancing
            ("anim@mp_player_intupperair_shagging", "idle_a"),
            ("anim@mp_player_intupperuncle_disco", "idle_a"),
            ("anim@mp_player_intupperfind_the_tensor", "idle_a"),
            ("anim@mp_player_intupperpeace", "idle_a"),
        };

        public FrozenDynamicMod()
        {
            // Инициализация меню
            _menu = new NativeMenu("Frozen Dynamic", "Управление NPC");

            _freezeItem = new NativeItem("Заморозить NPC", "Остановить всех пешеходов на месте");
            _unfreezeItem = new NativeItem("Разморозить NPC", "Вернуть NPC к обычному поведению");

            _menu.Add(_freezeItem);
            _menu.Add(_unfreezeItem);

            // Подписка на события
            _freezeItem.Activated += OnFreezeActivated;
            _unfreezeItem.Activated += OnUnfreezeActivated;

            Tick += OnTick;
            KeyDown += OnKeyDown;

            // Логирование успешной загрузки
            Log("Мод успешно загружен");
            ShowNotification("~g~Frozen Dynamic Mod~w~\nНажмите ~b~K~w~ для открытия меню");
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == MenuToggleKey)
            {
                _menu.Visible = !_menu.Visible;
                e.SuppressKeyPress = true;
            }
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (_menu.Visible)
            {
                _menu.Process();
            }

            // Tick-based maintenance для замороженных педов
            if (_isFrozen)
            {
                _tickCounter++;
                if (_tickCounter >= MAINTAIN_INTERVAL)
                {
                    _tickCounter = 0;
                    MaintainFrozenState();
                }
            }
        }

        private void OnFreezeActivated(object sender, EventArgs e)
        {
            if (_isFrozen)
            {
                ShowNotification("~y~NPC уже заморожены!");
                return;
            }

            int count = FreezeAllPeds();
            _isFrozen = true;

            Log($"Заморожено NPC: {count} (сохранено анимаций: {_pedAnimStates.Count})");
            ShowNotification($"~g~Заморожено NPC: {count}");
        }

        private void OnUnfreezeActivated(object sender, EventArgs e)
        {
            if (!_isFrozen)
            {
                ShowNotification("~y~NPC уже разморожены!");
                return;
            }

            int count = UnfreezeAllPeds();
            _isFrozen = false;

            Log($"Разморожено NPC: {count}");
            ShowNotification($"~g~Разморожено NPC: {count}");
        }

        /// <summary>
        /// Определяет текущую танцевальную анимацию педа
        /// </summary>
        private (string dict, string anim)? GetCurrentDanceAnim(Ped ped)
        {
            foreach (var (dict, anim) in DanceAnims)
            {
                Function.Call(Hash.REQUEST_ANIM_DICT, dict);

                if (Function.Call<bool>(Hash.IS_ENTITY_PLAYING_ANIM, ped.Handle, dict, anim, 3))
                {
                    return (dict, anim);
                }
            }

            // Проверяем сценарные анимации (WORLD_HUMAN_*)
            if (Function.Call<bool>(Hash.IS_PED_USING_ANY_SCENARIO, ped.Handle))
            {
                // GET_PED_SCENARIO_NAME hash = 0x3B0B693D
                string scenarioName = Function.Call<string>((Hash)0x3B0B693D, ped.Handle);
                if (!string.IsNullOrEmpty(scenarioName))
                {
                    return (null, scenarioName); // dict = null означает сценарий
                }
            }

            return null;
        }

        /// <summary>
        /// Замораживает всех педов
        /// </summary>
        private int FreezeAllPeds()
        {
            int count = 0;
            Ped playerPed = Game.Player.Character;

            if (playerPed == null || !playerPed.Exists())
            {
                ShowNotification("~y~Игрок не найден");
                return 0;
            }

            Ped[] allPeds = World.GetAllPeds();

            if (allPeds == null || allPeds.Length == 0)
            {
                ShowNotification("~y~NPC не найдены в игре");
                return 0;
            }

            foreach (Ped ped in allPeds)
            {
                // Пропускаем null, несуществующих и игрока
                if (ped == null || !ped.Exists() || ped == playerPed)
                    continue;

                try
                {
                    // Сохраняем анимацию (если это танец или сценарий)
                    var currentAnim = GetCurrentDanceAnim(ped);
                    if (currentAnim.HasValue)
                    {
                        double progress = 0.0;

                        if (currentAnim.Value.dict != null)
                        {
                            // Обычная анимация — сохраняем прогресс
                            progress = Function.Call<double>(
                                Hash.GET_ENTITY_ANIM_CURRENT_TIME, ped.Handle, currentAnim.Value.dict, currentAnim.Value.anim);
                        }

                        _pedAnimStates[ped.Handle] = new PedAnimState
                        {
                            Dict = currentAnim.Value.dict,
                            Anim = currentAnim.Value.anim,
                            Progress = progress,
                            IsScenario = currentAnim.Value.dict == null
                        };

                        Log($"Сохранена анимация для педа {ped.Handle}: {currentAnim.Value.dict ?? "SCENARIO"}/{currentAnim.Value.anim}");
                    }

                    // Обнуляем скорость перед заморозкой (чтобы физика не накапливалась)
                    Function.Call(Hash.SET_ENTITY_VELOCITY, ped.Handle, 0f, 0f, 0f);

                    // Замораживаем позицию
                    ped.IsPositionFrozen = true;

                    // Блокируем permanent events
                    ped.BlockPermanentEvents = true;

                    // === ПОЛНЫЙ ИГНОР — УСТАНАВЛИВАЕТСЯ ОДИН РАЗ ===

                    // Блокируем non-temporary events — пед не реагирует на AI-триггеры
                    Function.Call(Hash.SET_BLOCKING_OF_NON_TEMPORARY_EVENTS, ped.Handle, true);

                    // Relationship — полный игнор
                    SetPedIgnoredByEveryone(ped);

                    // Flee — не убегать (0, 0 = все flee атрибуты отключены)
                    Function.Call(Hash.SET_PED_FLEE_ATTRIBUTES, ped.Handle, 0, 0);

                    // Combat — не драться
                    Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped.Handle, 5, true);  // BF_CanFight = false
                    Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped.Handle, 46, true); // BF_CanFleeInCombat = false

                    // Добавляем в HashSet замороженных
                    _frozenPeds.Add(ped.Handle);

                    count++;
                }
                catch (Exception ex)
                {
                    Log($"Ошибка при заморозке педа {ped.Handle}: {ex.Message}");
                }
            }

            return count;
        }

        /// <summary>
        /// Размораживает всех педов
        /// Педы остаются стоять с полным игнором, танцы перезапускаются
        /// </summary>
        private int UnfreezeAllPeds()
        {
            int count = 0;
            Ped playerPed = Game.Player.Character;

            if (playerPed == null || !playerPed.Exists())
            {
                ShowNotification("~y~Игрок не найден");
                _frozenPeds.Clear();
                _pedAnimStates.Clear();
                return 0;
            }

            foreach (int handle in _frozenPeds.ToList()) // ToList для безопасного удаления
            {
                try
                {
                    Ped ped = (Ped)GTA.Entity.FromHandle(handle);

                    if (ped == null || !ped.Exists())
                    {
                        _frozenPeds.Remove(handle);
                        _pedAnimStates.Remove(handle);
                        continue;
                    }

                    // === 1. СНИМАЕМ ЗАМОРОЗКУ ===
                    ped.IsPositionFrozen = false;

                    // === 2. ОБНУЛЯЕМ СКОРОСТЬ ===
                    Function.Call(Hash.SET_ENTITY_VELOCITY, ped.Handle, 0f, 0f, 0f);

                    // === 3. РАЗБЛОКИРОВКА PERMANENT EVENTS ===
                    ped.BlockPermanentEvents = false;

                    // === 4. УСТАНАВЛИВАЕМ ИГНОР (пед стоит на месте) ===
                    SetPedIgnoredByEveryone(ped);
                    Function.Call(Hash.SET_BLOCKING_OF_NON_TEMPORARY_EVENTS, ped.Handle, true);
                    Function.Call(Hash.SET_PED_FLEE_ATTRIBUTES, ped.Handle, 0, 0);
                    Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped.Handle, 5, true);
                    Function.Call(Hash.SET_PED_COMBAT_ATTRIBUTES, ped.Handle, 46, true);

                    // === 5. ВОССТАНАВЛИВАЕМ АНИМАЦИЮ ===
                    RestorePedAnimation(ped);

                    _frozenPeds.Remove(handle);
                    count++;
                }
                catch (Exception ex)
                {
                    Log($"Ошибка при разморозке педа {handle}: {ex.Message}");
                    _frozenPeds.Remove(handle);
                    _pedAnimStates.Remove(handle);
                }
            }

            _frozenPeds.Clear();
            _pedAnimStates.Clear();
            return count;
        }

        /// <summary>
        /// Восстанавливает/перезапускает сохранённую анимацию педа
        /// </summary>
        private void RestorePedAnimation(Ped ped)
        {
            int handle = ped.Handle;

            if (!_pedAnimStates.TryGetValue(handle, out PedAnimState animState))
                return;

            try
            {
                if (animState.IsScenario && animState.Anim != null)
                {
                    // Сценарная анимация — не можем перезапустить без сценария
                    // Пед будет продолжать сам
                    Log($"Пед {handle} был в сценарии {animState.Anim}, не перезапускаем");
                }
                else if (animState.Dict != null && animState.Anim != null)
                {
                    // Обычная анимация — перезапускаем
                    Function.Call(Hash.REQUEST_ANIM_DICT, animState.Dict);

                    int waitCount = 0;
                    while (!Function.Call<bool>(Hash.HAS_ANIM_DICT_LOADED, animState.Dict) && waitCount < 50)
                    {
                        Script.Wait(10);
                        waitCount++;
                    }

                    // Запускаем анимацию заново
                    Function.Call(Hash.TASK_PLAY_ANIM,
                        ped.Handle,
                        animState.Dict,
                        animState.Anim,
                        8.0f,           // blendInSpeed
                        -8.0f,          // blendOutSpeed
                        -1,             // duration (-1 = loop)
                        1,              // flags (1 = LOOP)
                        1.0f,           // playbackRate
                        false,          // lockX
                        false,          // lockY
                        false           // lockZ
                    );

                    Script.Wait(50);

                    // Восстанавливаем прогресс анимации
                    Function.Call(Hash.SET_ENTITY_ANIM_CURRENT_TIME,
                        ped.Handle, animState.Dict, animState.Anim, animState.Progress);

                    Log($"Восстановлена анимация для педа {handle}: {animState.Dict}/{animState.Anim} (progress: {animState.Progress:F2})");
                }
            }
            catch (Exception ex)
            {
                Log($"Ошибка при восстановлении анимации педа {handle}: {ex.Message}");
            }
        }

        /// <summary>
        /// Поддерживает состояние замороженных педов (вызывается каждые ~500ms)
        /// ТОЛЬКО для проверки мёртвых/удалённых педов — не для установки флагов
        /// </summary>
        private void MaintainFrozenState()
        {
            foreach (int handle in _frozenPeds.ToList())
            {
                try
                {
                    Ped ped = (Ped)GTA.Entity.FromHandle(handle);

                    if (ped == null || !ped.Exists() || ped.IsDead)
                    {
                        _frozenPeds.Remove(handle);
                        _pedAnimStates.Remove(handle);
                    }
                    else
                    {
                        // Просто убеждаемся что позиция всё ещё заморожена
                        ped.IsPositionFrozen = true;
                    }
                }
                catch (Exception ex)
                {
                    Log($"Ошибка при проверке педа {handle}: {ex.Message}");
                    _frozenPeds.Remove(handle);
                    _pedAnimStates.Remove(handle);
                }
            }
        }

        /// <summary>
        /// Устанавливает полный игнор игрока для педа
        /// </summary>
        private void SetPedIgnoredByEveryone(Ped ped)
        {
            try
            {
                Ped playerPed = Game.Player.Character;
                if (playerPed == null || !playerPed.Exists())
                    return;

                int playerGroup = Function.Call<int>(Hash.GET_PED_RELATIONSHIP_GROUP_HASH, playerPed.Handle);
                int pedGroup = Function.Call<int>(Hash.GET_PED_RELATIONSHIP_GROUP_HASH, ped.Handle);

                // Relationship level 5 = Companion/Ignore (максимальный игнор)
                Function.Call(Hash.SET_RELATIONSHIP_BETWEEN_GROUPS, 5, pedGroup, playerGroup);
                Function.Call(Hash.SET_RELATIONSHIP_BETWEEN_GROUPS, 5, playerGroup, pedGroup);
            }
            catch (Exception ex)
            {
                Log($"Ошибка при установке IgnoredByEveryone: {ex.Message}");
            }
        }

        /// <summary>
        /// Состояние анимации педа
        /// </summary>
        private class PedAnimState
        {
            public string Dict { get; set; }
            public string Anim { get; set; }
            public double Progress { get; set; }
            public bool IsScenario { get; set; }
        }

        private void Log(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[FrozenDynamic] {message}");
        }

        private void ShowNotification(string message)
        {
            try
            {
                GTA.UI.Notification.Show(message);
            }
            catch (Exception ex)
            {
                Log($"Ошибка при показе уведомления: {ex.Message}");
            }
        }
    }
}
