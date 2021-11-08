using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using GlobalEnums;
using Modding;
using MonoMod.Utils;
using UnityEngine;

namespace MenderbugsRevenge
{
    public class MenderbugsRevenge : Mod, IGlobalSettings<GlobalSettings>, IMenuMod
    {
        internal static MenderbugsRevenge instance;
        public MenderbugsRevenge() : base("Menderbug's Revenge")
        {
            instance = this;
        }
        public override string GetVersion() => "1.2";

        public static GlobalSettings GS = new GlobalSettings();
        public GlobalSettings OnSaveGlobal() => GS;
        public void OnLoadGlobal(GlobalSettings s) => GS = s;

        public override void Initialize()
        {
            Log("Initializing...");
            BreakableWatcher.Hook();
            BreakableWatcher.OnBrokeObject += OnBrokeObject;

            GameObject go = new GameObject();
            UnityEngine.Object.DontDestroyOnLoad(go);
            _coroutineStarter = go.AddComponent<NonBouncer>();
            MarkedForDeath = false;

            ModHooks.AfterPlayerDeadHook += CancelPriorDeaths;
        }

        public bool ToggleButtonInsideMenu => false;
        public List<IMenuMod.MenuEntry> GetMenuData(IMenuMod.MenuEntry? wrappedToggleButtonEntry)
        {
            List<IMenuMod.MenuEntry> menuEntries = new List<IMenuMod.MenuEntry>();

            menuEntries.Add(new IMenuMod.MenuEntry
            {
                Name = "Action on break",
                Description = "Choose what happens when you break an object",
                Values = Enum.GetNames(typeof(GlobalSettings.PunishmentType)).Select(x => Regex.Replace(x, "([A-Z])", " $1").TrimStart()).ToArray(),
                Saver = i => GS.ActionOnBreak = (GlobalSettings.PunishmentType)i,
                Loader = () => (int)GS.ActionOnBreak
            });

            return menuEntries;
        }

        private bool MarkedForDeath = false;
        private static readonly FastReflectionDelegate HeroController_CanTakeDamage = typeof(HeroController)
                                                                                     .GetMethod("CanTakeDamage", BindingFlags.NonPublic | BindingFlags.Instance)
                                                                                     .GetFastDelegate();
        private NonBouncer _coroutineStarter;

        private void CancelPriorDeaths()
        {
            _coroutineStarter.StopAllCoroutines();
            if (MarkedForDeath) Log("Removing mark");
            MarkedForDeath = false;
        }

        private void OnBrokeObject(string msg)
        {
            Log($"Broke Object: {msg}");

            switch (GS.ActionOnBreak)
            {
                case GlobalSettings.PunishmentType.OneDamage:
                    DealOneDamage(); 
                    return;
                case GlobalSettings.PunishmentType.Die:
                    Die();
                    return;
            }
        }

        private void DealOneDamage()
        {
            HeroController.instance.TakeDamage(null, CollisionSide.bottom, 1, 1);
        }
        private void Die()
        {
            if (MarkedForDeath)
            {
                Log("Already marked!");
                return;
            }

            if ((bool)HeroController_CanTakeDamage(HeroController.instance))
            {
                Log("Killing immediately");
                KillHero();
                return;
            }

            Log("Marking for death...");
            MarkedForDeath = true;
            PlayFuryAudio();
            _coroutineStarter.StartCoroutine(DieWhenAble());
        }

        private IEnumerator DieWhenAble()
        {
            yield return new WaitUntil(() => (bool)HeroController_CanTakeDamage(HeroController.instance) || GS.ActionOnBreak != GlobalSettings.PunishmentType.Die);
            if (GS.ActionOnBreak == GlobalSettings.PunishmentType.Die) KillHero();

        }
        private void KillHero()
        {
            MarkedForDeath = false;
            StopFuryAudio();
            HeroController.instance.TakeDamage(null, CollisionSide.bottom, 61, (int)GlobalEnums.HazardType.ACID);
            _coroutineStarter.StopAllCoroutines();
        }
        private void PlayFuryAudio()
        {
            HeroController.instance.transform.Find("Charm Effects").Find("Fury").gameObject.LocateMyFSM("Control Audio").SendEvent("PLAY");
        }
        private void StopFuryAudio()
        {
            HeroController.instance.transform.Find("Charm Effects").Find("Fury").gameObject.LocateMyFSM("Control Audio").SendEvent("STOP");
        }


    }
}
