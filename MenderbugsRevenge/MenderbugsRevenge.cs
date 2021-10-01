using System;
using System.Collections;
using System.Reflection;
using HutongGames.PlayMaker;
using Modding;
using MonoMod.Cil;
using MonoMod.Utils;
using UnityEngine;
using Vasi;

namespace MenderbugsRevenge
{
    public class MenderbugsRevenge : Mod, ITogglableMod
    {
        public MenderbugsRevenge() : base("Menderbug's Revenge") { }
        internal static MenderbugsRevenge instance;
        public override string GetVersion() => "0.9";

        public override void Initialize()
        {
            Log("Initializing...");
            instance = this;

            On.Breakable.Break += TriggerBreakable;
            IL.BreakablePoleSimple.OnTriggerEnter2D += TriggerBreakablePoleActivated;
            IL.BreakableInfectedVine.OnTriggerEnter2D += TriggerBreakableVineActivated;
            IL.InfectedBurstLarge.OnTriggerEnter2D += TriggerInfectionBubble;
            On.JellyEgg.Burst += TriggerJellyEgg;

            IL.GrassCut.OnTriggerEnter2D += TriggerGrass;
            IL.TownGrass.OnTriggerEnter2D += TriggerGrass;
            IL.GrassSpriteBehaviour.OnTriggerEnter2D += TriggerGrass;

            On.PlayMakerFSM.OnEnable += TriggerFsmBreakables;

            GameObject go = new GameObject();
            UnityEngine.Object.DontDestroyOnLoad(go);
            _coroutineStarter = go.AddComponent<NonBouncer>();
            MarkedForDeath = false;

            ModHooks.AfterPlayerDeadHook += CancelPriorDeaths;
        }

        public void Unload()
        {
            On.Breakable.Break -= TriggerBreakable;
            IL.BreakablePoleSimple.OnTriggerEnter2D -= TriggerBreakablePoleActivated;
            IL.BreakableInfectedVine.OnTriggerEnter2D -= TriggerBreakableVineActivated;
            IL.InfectedBurstLarge.OnTriggerEnter2D -= TriggerInfectionBubble;
            On.JellyEgg.Burst -= TriggerJellyEgg;

            IL.GrassCut.OnTriggerEnter2D -= TriggerGrass;
            IL.TownGrass.OnTriggerEnter2D -= TriggerGrass;
            IL.GrassSpriteBehaviour.OnTriggerEnter2D -= TriggerGrass;

            On.PlayMakerFSM.OnEnable -= TriggerFsmBreakables;

            UnityEngine.Object.Destroy(_coroutineStarter.gameObject);
            MarkedForDeath = false;

            ModHooks.AfterPlayerDeadHook -= CancelPriorDeaths;
        }

        private void BrokeObject(string msg, GameObject go = null)
        {
            if (go != null)
            {
                foreach (Collider2D col in go.GetComponentsInChildren<Collider2D>())
                {
                    if (col.gameObject.layer == (int)GlobalEnums.PhysLayers.TERRAIN)
                    {
                        return;
                    }
                }
            }

            Die(msg);
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

        private void Die(string msg)
        {
            Log($"Broke Object: {msg}");

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
            yield return new WaitUntil(() => (bool)HeroController_CanTakeDamage(HeroController.instance));
            KillHero();

        }
        private void KillHero()
        {
            MarkedForDeath = false;
            StopFuryAudio();
            HeroController.instance.TakeDamage(HeroController.instance.gameObject, GlobalEnums.CollisionSide.bottom, 61, (int)GlobalEnums.HazardType.ACID);
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

        private void TriggerGrass(ILContext il)
        {
            ILCursor cursor = new ILCursor(il);

            if (cursor.TryGotoNext
            (
                MoveType.After,
                i => i.MatchLdarg(1),
                i => i.MatchCall<GrassCut>("ShouldCut")
            ))
            {
                cursor.EmitDelegate<Func<bool, bool>>((shouldCut) => { if (shouldCut) BrokeObject("Grass"); return shouldCut; });
            }
        }
        private void TriggerInfectionBubble(ILContext il)
        {
            ILCursor cursor = new ILCursor(il);

            if (cursor.TryGotoNext
            (
                MoveType.After,
                i => i.MatchLdarg(0),
                i => i.MatchLdfld<InfectedBurstLarge>("audioSource"),
                i => i.MatchCallvirt<AudioSource>("Play")
            ))
            {
                cursor.EmitDelegate<Action>(() => { BrokeObject("Bubble"); });
            }
        }
        private void TriggerBreakablePoleActivated(ILContext il)
        {
            ILCursor cursor = new ILCursor(il);

            if (cursor.TryGotoNext
            (
                i => i.MatchLdarg(0),
                i => i.MatchLdcI4(1),
                i => i.MatchStfld<BreakablePoleSimple>("activated")
            ))
            {
                cursor.EmitDelegate<Action>(() => { BrokeObject("Pole"); });
            }
        }
        private void TriggerBreakableVineActivated(ILContext il)
        {
            ILCursor cursor = new ILCursor(il);

            if (cursor.TryGotoNext
            (
                i => i.MatchLdarg(0),
                i => i.MatchLdcI4(1),
                i => i.MatchStfld<BreakableInfectedVine>("activated")
            ))
            {
                cursor.EmitDelegate<Action>(() => { BrokeObject("Infected Vine"); });
            }
        }
        private void TriggerJellyEgg(On.JellyEgg.orig_Burst orig, JellyEgg self)
        {
            orig(self); BrokeObject("Jelly Egg");
        }
        private void TriggerBreakable(On.Breakable.orig_Break orig, Breakable self, float flingAngleMin, float flingAngleMax, float impactMultiplier)
        {
            if (!Modding.ReflectionHelper.GetField<Breakable, bool>(self, "isBroken"))
            {
                BrokeObject($"Normal: {self.gameObject.name}", self.gameObject);
            }
            orig(self, flingAngleMin, flingAngleMax, impactMultiplier);
        }
        private void TriggerFsmBreakables(On.PlayMakerFSM.orig_OnEnable orig, PlayMakerFSM self)
        {
            orig(self);

            if (self.FsmName == "FSM")
            {
                if (self.TryGetState("Spider Egg?", out FsmState spiderEgg))
                {
                    spiderEgg.InsertMethod(0, () => BrokeObject($"FSM: {self.gameObject.name}", self.gameObject));
                }
            }
        }
    }
}
