using System;
using HutongGames.PlayMaker;
using MonoMod.Cil;
using UnityEngine;
using Vasi;

namespace MenderbugsRevenge
{
    public static class BreakableWatcher
    {
        /// <summary>
        /// Raised when the player breaks any object. String is a message that describes the object broken.
        /// </summary>
        public static event Action<string> OnBrokeObject;

        public static void Hook()
        {
            On.Breakable.Break += TriggerBreakable;
            IL.BreakablePoleSimple.OnTriggerEnter2D += TriggerBreakablePoleActivated;
            IL.BreakableInfectedVine.OnTriggerEnter2D += TriggerBreakableVineActivated;
            IL.InfectedBurstLarge.OnTriggerEnter2D += TriggerInfectionBubble;
            On.JellyEgg.Burst += TriggerJellyEgg;

            IL.GrassCut.OnTriggerEnter2D += TriggerGrass;
            IL.TownGrass.OnTriggerEnter2D += TriggerGrass;
            IL.GrassSpriteBehaviour.OnTriggerEnter2D += TriggerGrass;

            On.PlayMakerFSM.OnEnable += TriggerFsmBreakables;
        }

        public static void Unhook()
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
        }

        private static void BrokeObject(string msg, GameObject go = null)
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

            OnBrokeObject?.Invoke(msg);
        }

        private static void TriggerGrass(ILContext il)
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
        private static void TriggerInfectionBubble(ILContext il)
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
        private static void TriggerBreakablePoleActivated(ILContext il)
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
        private static void TriggerBreakableVineActivated(ILContext il)
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
        private static void TriggerJellyEgg(On.JellyEgg.orig_Burst orig, JellyEgg self)
        {
            orig(self); BrokeObject("Jelly Egg");
        }
        private static void TriggerBreakable(On.Breakable.orig_Break orig, Breakable self, float flingAngleMin, float flingAngleMax, float impactMultiplier)
        {
            if (!Modding.ReflectionHelper.GetField<Breakable, bool>(self, "isBroken"))
            {
                BrokeObject($"Normal: {self.gameObject.name}", self.gameObject);
            }
            orig(self, flingAngleMin, flingAngleMax, impactMultiplier);
        }
        private static void TriggerFsmBreakables(On.PlayMakerFSM.orig_OnEnable orig, PlayMakerFSM self)
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
