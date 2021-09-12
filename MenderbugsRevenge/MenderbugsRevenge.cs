using System;
using System.Collections.Generic;
using Modding;
using MonoMod.Cil;
using UnityEngine;

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

            ModHooks.SetPlayerBoolHook += TriggerMenderSign;
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

            ModHooks.SetPlayerBoolHook -= TriggerMenderSign;
        }

        private static void BrokeObject(GameObject go = null)
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

            Die();
        }

        private static void Die()
        {
            HeroController.instance.TakeDamage(HeroController.instance.gameObject, GlobalEnums.CollisionSide.bottom, 61, 0);
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
                cursor.EmitDelegate<Func<bool, bool>>((shouldCut) => { if (shouldCut) BrokeObject(); return shouldCut; });
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
                cursor.EmitDelegate<Action>(() => { BrokeObject(); });
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
                cursor.EmitDelegate<Action>(() => { BrokeObject(); });
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
                cursor.EmitDelegate<Action>(() => { BrokeObject(); });
            }
        }
        private static void TriggerJellyEgg(On.JellyEgg.orig_Burst orig, JellyEgg self)
        {
            orig(self); BrokeObject();
        }
        private static void TriggerBreakable(On.Breakable.orig_Break orig, Breakable self, float flingAngleMin, float flingAngleMax, float impactMultiplier)
        {
            if (!ReflectionHelper.GetField<Breakable, bool>(self, "isBroken"))
            {
                BrokeObject(self.gameObject);
            }
            orig(self, flingAngleMin, flingAngleMax, impactMultiplier);
        }
        private bool TriggerMenderSign(string name, bool orig)
        {
            if (name == nameof(PlayerData.menderSignBroken) && GameManager.instance.sceneName == "Crossroads_01" && orig) BrokeObject();
            return orig;
        }
    }
}
