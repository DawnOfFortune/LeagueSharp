﻿#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.Remoting.Messaging;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;
using Color = System.Drawing.Color;
#endregion

namespace TwistedFate
{
    internal class Program
    {
        private static Menu Config;

        private static Spell Q;
        private static float Qangle = 28*(float) Math.PI/180;
        private static Orbwalking.Orbwalker SOW;

        private static void Main(string[] args)
        {
            CustomEvents.Game.OnGameLoad += Game_OnGameLoad;
        }

        private static void Game_OnGameLoad(EventArgs args)
        {
            if (ObjectManager.Player.BaseSkinName != "TwistedFate") return;

            Q = new Spell(SpellSlot.Q, 1450);
            Q.SetSkillshot(0.25f, 40f, 1000f, false, Prediction.SkillshotType.SkillshotLine);

            //Make the menu
            Config = new Menu("Twisted Fate", "TwistedFate", true);

            var SowMenu = new Menu("Orbwalking", "Orbwalking");
            SOW = new Orbwalking.Orbwalker(SowMenu);
            Config.AddSubMenu(SowMenu);

            /* Q */
            var q = new Menu("Q - Wildcards", "Q");
            q.AddItem(new MenuItem("AutoQI", "Auto-Q immobile").SetValue(true));
            q.AddItem(new MenuItem("AutoQD", "Auto-Q dashing").SetValue(true));
            Config.AddSubMenu(q);

            /* W */
            var w = new Menu("W - Pick a card", "W");
            w.AddItem(
                new MenuItem("SelectYellow", "Select Yellow").SetValue(new KeyBind("W".ToCharArray()[0],
                    KeyBindType.Press)));
            w.AddItem(
                new MenuItem("SelectBlue", "Select Blue").SetValue(new KeyBind("E".ToCharArray()[0], KeyBindType.Press)));
            w.AddItem(
                new MenuItem("SelectRed", "Select Red").SetValue(new KeyBind("T".ToCharArray()[0], KeyBindType.Press)));
            Config.AddSubMenu(w);

            var r = new Menu("R - Destiny", "R");
            r.AddItem(new MenuItem("AutoY", "Select yellow card after R").SetValue(true));
            Config.AddSubMenu(r);

            /*Drawing*/
            var Drawings = new Menu("Drawings", "Drawings");
            Drawings.AddItem(new MenuItem("Qcircle", "Q Range").SetValue(new Circle(true, Color.FromArgb(100,255,0,255))));
            Drawings.AddItem(new MenuItem("Rcircle", "R Range").SetValue(new Circle(true, Color.FromArgb(100, 255, 255, 255))));
            Drawings.AddItem(new MenuItem("Rcircle2", "R Range (minimap)").SetValue(new Circle(true, Color.FromArgb(255, 255, 255, 255))));

            Config.AddSubMenu(Drawings);

            Config.AddItem(new MenuItem("Combo", "Combo").SetValue(new KeyBind(32, KeyBindType.Press)));

            Config.AddToMainMenu();

            Game.OnGameUpdate += Game_OnGameUpdate;
            Drawing.OnDraw += Drawing_OnDraw;
            Obj_AI_Hero.OnProcessSpellCast += Obj_AI_Hero_OnProcessSpellCast;
        }

        static void Obj_AI_Hero_OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (sender.IsMe && args.SData.Name == "gate" && Config.Item("AutoY").GetValue<bool>())
            {
                CardSelector.StartSelecting(Cards.Yellow);
            }
        }

        static void Drawing_OnDraw(EventArgs args)
        {
            var qCircle = Config.Item("Qcircle").GetValue<Circle>();
            var rCircle = Config.Item("Rcircle").GetValue<Circle>();
            var rCircle2 = Config.Item("Rcircle2").GetValue<Circle>();

            if (qCircle.Enabled)
            {
                Utility.DrawCircle(ObjectManager.Player.Position, Q.Range, qCircle.Color);
            }

            if (rCircle.Enabled)
            {
                Utility.DrawCircle(ObjectManager.Player.Position, 5500, rCircle.Color);
            }

            if (rCircle2.Enabled)
            {
                Utility.DrawCircle(ObjectManager.Player.Position, 5500, rCircle2.Color, 1, 30, true);
            }
        }


        private static int CountHits(Vector2 position, List<Vector2> points, List<int> hitBoxes)
        {
            var result = 0;

            var startPoint = ObjectManager.Player.ServerPosition.To2D();
            var originalDirection = Q.Range*(position - startPoint).Normalized();
            var originalEndPoint = startPoint + originalDirection;

            for (var i = 0; i < points.Count; i++)
            {
                var point = points[i];

                for (var k = 0; k < 3; k++)
                {
                    var endPoint = new Vector2();
                    if (k == 0) endPoint = originalEndPoint;
                    if (k == 1) endPoint = startPoint + originalDirection.Rotated(Qangle);
                    if (k == 2) endPoint = startPoint + originalDirection.Rotated(-Qangle);

                    if (point.Distance(startPoint, endPoint, true, true) <
                        (Q.Width + hitBoxes[i])*(Q.Width + hitBoxes[i]))
                    {
                        result++;
                        break;
                    }
                }
            }

            return result;
        }

        private static void CastQ(Obj_AI_Base unit, Vector2 unitPosition, int minTargets = 0)
        {
            var points = new List<Vector2>();
            var hitBoxes = new List<int>();

            var startPoint = ObjectManager.Player.ServerPosition.To2D();
            var originalDirection = Q.Range*(unitPosition - startPoint).Normalized();

            foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>())
            {
                if (enemy.IsValidTarget() && enemy.NetworkId != unit.NetworkId)
                {
                    var pos = Q.GetPrediction(enemy);
                    if (pos.HitChance >= Prediction.HitChance.LowHitchance)
                    {
                        points.Add(pos.Position.To2D());
                        hitBoxes.Add((int) enemy.BoundingRadius);
                    }
                }
            }


            var posiblePositions = new List<Vector2>();

            for (var i = 0; i < 3; i++)
            {
                if (i == 0) posiblePositions.Add(unitPosition + originalDirection.Rotated(0));
                if (i == 1) posiblePositions.Add(startPoint + originalDirection.Rotated(Qangle));
                if (i == 2) posiblePositions.Add(startPoint + originalDirection.Rotated(-Qangle));
            }


            if (startPoint.Distance(unitPosition) < 900)
            {
                for (var i = 0; i < 3; i++)
                {
                    var pos = posiblePositions[i];
                    var direction = (pos - startPoint).Normalized().Perpendicular();
                    var k = (2/3*(unit.BoundingRadius + Q.Width));
                    posiblePositions.Add(startPoint - k*direction);
                    posiblePositions.Add(startPoint + k*direction);
                }
            }

            var bestPosition = new Vector2();
            var bestHit = -1;

            foreach (var position in posiblePositions)
            {
                var hits = CountHits(position, points, hitBoxes);
                if (hits > bestHit)
                {
                    bestPosition = position;
                    bestHit = hits;
                }
            }

            if (bestHit + 1 <= minTargets)
                return;

            Q.Cast(bestPosition.To3D(), true);
        }

        private static float ComboDamage(Obj_AI_Hero hero)
        {   
            var dmg = 0d;
            dmg += DamageLib.getDmg(hero, DamageLib.SpellType.Q);
            dmg += DamageLib.getDmg(hero, DamageLib.SpellType.W, DamageLib.StageType.ThirdDamage);
            dmg += DamageLib.getDmg(hero, DamageLib.SpellType.E, DamageLib.StageType.ThirdDamage);



            if (Items.HasItem("ItemBlackfireTorch"))
            {
                dmg += DamageLib.getDmg(hero, DamageLib.SpellType.DFG);
                dmg = dmg * 1.2;
            }

            if(ObjectManager.Player.GetSpellSlot("SummonerIgnite") != SpellSlot.Unknown)
            {
                dmg += DamageLib.getDmg(hero, DamageLib.SpellType.IGNITE);
            }

            return (float) dmg;
        }

        private static void Game_OnGameUpdate(EventArgs args)
        {

            SOW.SetAttacks(CardSelector.Status != SelectStatus.Selecting && Environment.TickCount - CardSelector.LastWSent > 300);
            var combo = Config.Item("Combo").GetValue<KeyBind>().Active;

            //Select cards.
            if (Config.Item("SelectYellow").GetValue<KeyBind>().Active ||
                combo)
            {
                CardSelector.StartSelecting(Cards.Yellow);
            }

            if (Config.Item("SelectBlue").GetValue<KeyBind>().Active)
            {
                CardSelector.StartSelecting(Cards.Blue);
            }

            if (Config.Item("SelectRed").GetValue<KeyBind>().Active)
            {
                CardSelector.StartSelecting(Cards.Red);
            }

            if (CardSelector.Status == SelectStatus.Selected && combo)
            {
                var target = SOW.GetTarget();
                if (target.IsValidTarget() && target is Obj_AI_Hero && Items.HasItem("ItemBlackfireTorch") && ComboDamage((Obj_AI_Hero) target) >= target.Health)
                {
                    Items.UseItem("ItemBlackfireTorch", (Obj_AI_Hero) target);
                }
            }

            //Auto Q
            var autoQI = Config.Item("AutoQI").GetValue<bool>();
            var autoQD = Config.Item("AutoQD").GetValue<bool>();

            
            if (ObjectManager.Player.Spellbook.CanUseSpell(SpellSlot.Q) == SpellState.Ready && (autoQD || autoQI))
                foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>())
                {
                    if (enemy.IsValidTarget(Q.Range * 2))
                    {
                        var pred = Q.GetPrediction(enemy);
                        if ((pred.HitChance == Prediction.HitChance.Immobile && autoQI) ||
                            (pred.HitChance == Prediction.HitChance.Dashing  && autoQD))
                        {
                            CastQ(enemy, pred.Position.To2D());
                        }
                    }
                   
                }

        }
    }
}