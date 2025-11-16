#region References
using System;
using System.Collections;
using Server;
using Server.Items;                    // <-- BasePotion, PotionEffect
using Server.Network;
using Server.Spells;
using Server.Targeting;
using Server.Custom.Engineering.Grenadier; // <-- GrenadierConfig
#endregion


namespace Server.Custom.Engineering.Grenadier.Bombs
{
	public abstract class BaseMegaBomb : BasePotion
	{
		private static readonly bool LeveledExplosion = true; // Should explosion potions explode other nearby potions?
		private static readonly bool InstantExplosion = true; // Should explosion potions explode on impact?
		private static readonly bool RelativeLocation = true; // Is the explosion target location relative for mobiles?
		private static readonly TimeSpan ReuseDelay = GrenadierConfig.BombReuseDelay;
		private DateTime _nextUse;
		private const int ExplosionRange = 8; // How long is the blast radius?
		private Timer m_Timer;
		private ArrayList m_Users;

		public BaseMegaBomb(PotionEffect effect)
			: base(0xF0D, effect)
		{ EnsureItemHue(); }

		public BaseMegaBomb(Serial serial)
			: base(serial)
		{ EnsureItemHue(); }

		public abstract int MinDamage { get; }
		public abstract int MaxDamage { get; }
		public override bool RequireFreeHand { get { return false; } }
		
		// ---- Tier hooks (add to each base) ----
		public virtual int ExplosionRadius { get { return  (/* default */ 2); } }   // Tactical=1, Strategic=3, Mega=4 (in child)
		public virtual TimeSpan FuseDelay { get { return TimeSpan.FromSeconds(1.0); } }
		public virtual int CooldownSeconds { get { return (int)Server.Custom.Engineering.Grenadier.GrenadierConfig.BombReuseDelay.TotalSeconds; } }

		// Distance-based damage scaling (1.0 = full, 0.0 = none). Override in Strategic/Mega for falloff.
		protected virtual double DamageFalloff(Point3D center, Point3D hit)
		{
			return 1.0; // default: no falloff
		}

		// Called after damage is applied; override for knockback/DoT/etc.
		protected virtual void OnDetonate(Mobile from, Point3D loc, Map map)
		{
			// default: no extra effect
		}
		public override void Serialize(GenericWriter writer)
		{
			base.Serialize(writer);

			writer.Write(0); // version
		}

		public override void Deserialize(GenericReader r)
		{
			base.Deserialize(r);
			r.ReadInt();
		    EnsureItemHue();
		}

		// --- Hue control (config-driven) ---
		public virtual int ExplosionHue
		{
			get { return Server.Custom.Engineering.Grenadier.GrenadierConfig.MegaHue; }
		}

		protected void EnsureItemHue()
		{
			if (Hue != ExplosionHue)
				Hue = ExplosionHue;
		}

		public virtual object FindParent(Mobile from)
		{
			Mobile m = HeldBy;

			if (m != null && m.Holding == this)
			{
				return m;
			}

			object obj = RootParent;

			if (obj != null)
			{
				return obj;
			}

			if (Map == Map.Internal)
			{
				return from;
			}

			return this;
		}

		public override void Drink(Mobile from)
		{
			if (!GrenadierRegionRules.CanUseBomb(from, damage: true))
			{
				from.SendMessage(38, "You cannot use damaging bombs in town.");
				return;
			}
			if (DateTime.UtcNow < _nextUse)
			{
				from.SendMessage(38, "You must wait a moment before using another bomb.");
				return;
			}

			if (Core.AOS && (from.Paralyzed || from.Frozen || (from.Spell != null && from.Spell.IsCasting)))
			{
				from.SendLocalizedMessage(1062725); // You can not use a purple potion while paralyzed.
				return;
			}

			ThrowTarget targ = from.Target as ThrowTarget;
			Stackable = false; // Scavenged explosion potions won't stack with those ones in backpack, and still will explode.

			if (targ != null && targ.Potion == this)
			{
				return;
			}

			from.RevealingAction();

			if (m_Users == null)
			{
				m_Users = new ArrayList();
			}

			if (!m_Users.Contains(from))
			{
				m_Users.Add(from);
			}

			from.Target = new ThrowTarget(this);

			// start reuse cooldown on commit
			_nextUse = DateTime.UtcNow + TimeSpan.FromSeconds(CooldownSeconds);



			if (m_Timer == null)
			{
				from.SendLocalizedMessage(500236); // You should throw it now!

				if (Core.ML)
				{
					int ticks = Math.Max(1, (int)Math.Round(FuseDelay.TotalSeconds));
					m_Timer = Timer.DelayCall(
						TimeSpan.FromSeconds(1.0),
						TimeSpan.FromSeconds(1.0),
						ticks + 1,
						new TimerStateCallback(Detonate_OnTick),
						new object[] { from, ticks }); // shows ticks, then explodes
				}
			}
		}

		public virtual void Explode(Mobile from, bool direct, Point3D loc, Map map)
		{
			if (Deleted)
			{
				return;
			}

			Consume();

			for (int i = 0; m_Users != null && i < m_Users.Count; ++i)
			{
				Mobile m = (Mobile)m_Users[i];
				ThrowTarget targ = m.Target as ThrowTarget;

				if (targ != null && targ.Potion == this)
				{
					Target.Cancel(m);
				}
			}

			if (map == null)
			{
				return;
			}

			Effects.PlaySound(loc, map, 0x307);

			Effects.SendLocationEffect(loc, map, 0x36B0, 9, 10, ExplosionHue, 0);

			int alchemyBonus = 0;

			if (direct)
			{
				alchemyBonus = (int)(from.Skills.Alchemy.Value / (Core.AOS ? 5 : 10));
			}

			int ar = ExplosionRadius;
			IPooledEnumerable eable = LeveledExplosion
				? map.GetObjectsInRange(loc, ar)
				: (IPooledEnumerable)map.GetMobilesInRange(loc, ar);
			ArrayList toExplode = new ArrayList();

			int toDamage = 0;

			foreach (object o in eable)
			{
				if (o is Mobile &&
					(from == null || (SpellHelper.ValidIndirectTarget(from, (Mobile)o) && from.CanBeHarmful((Mobile)o, false) && from.InLOS((Mobile)o))))
				{
					toExplode.Add(o);
					++toDamage;
				}
				else if (o is BaseMegaBomb && o != this)
				{
					toExplode.Add(o);
				}
			}

			eable.Free();

			int min = Scale(from, MinDamage);
			int max = Scale(from, MaxDamage);

			for (int i = 0; i < toExplode.Count; ++i)
			{
				object o = toExplode[i];

				if (o is Mobile)
				{
					Mobile m = (Mobile)o;

					if (from != null)
						from.DoHarmful(m);

					bool ally = from != null && from.AccessLevel == AccessLevel.Player &&
								m.AccessLevel == AccessLevel.Player &&
								from.Guild != null && from.Guild == m.Guild;
					if (!GrenadierConfig.FriendlyFire && ally)
						continue;

					int ep = AosAttributes.GetValue(from, AosAttribute.EnhancePotions);
					double scale = 1.0 + (ep > 0 ? ep : 0) / 100.0;
					double fall = DamageFalloff(loc, m.Location);
					int damage = (int)Math.Round(Server.Utility.RandomMinMax(MinDamage, MaxDamage) * scale * fall);
					AOS.Damage(m, from, damage, 0, 100, 0, 0, 0);
				}

				else if (o is BaseMegaBomb)
				{
					BaseMegaBomb pot = (BaseMegaBomb)o;

					pot.Explode(from, false, pot.GetWorldLocation(), pot.Map);
				}
			}
			OnDetonate(from, loc, map);

			// Ring animation (hued): emit a full wave out to the tier radius
			TriggerRingWave(map, loc, ExplosionRadius);

		}

		// private void TriggerFlamestrikeWave(Point3D center, Map map)
		// {
		// 	for (int radius = 0; radius <= ExplosionRange; radius++)
		// 	{
		// 		Timer.DelayCall(TimeSpan.FromMilliseconds(10 * radius), () =>
		// 		{
		// 			for (int x = -radius; x <= radius; x++)
		// 			{
		// 				for (int y = -radius; y <= radius; y++)
		// 				{
		// 					if (x * x + y * y <= radius * radius)
		// 					{
		// 						Point3D loc = new Point3D(center.X + x, center.Y + y, center.Z);
		// 						Effects.SendLocationEffect(loc, map, Server.Custom.Engineering.Grenadier.GrenadierConfig.WaveFxItemID, 30, 10, ExplosionHue, 0);

		// 					}
		// 				}
		// 			}
		// 		});
		// 	}
		// }
		private void TriggerRingWave(Map map, Point3D center, int radius)
		{
			if (map == null || radius <= 0) return;

			// Use hue-friendly tile (configurable)
			int waveID = Server.Custom.Engineering.Grenadier.GrenadierConfig.WaveFxItemID;

			for (int r = 1; r <= radius; r++)
			{
				// 8 directions + 8 diagonals (16 points per ring)
				Emit(center.X + r, center.Y,       center.Z);
				Emit(center.X - r, center.Y,       center.Z);
				Emit(center.X,     center.Y + r,   center.Z);
				Emit(center.X,     center.Y - r,   center.Z);

				Emit(center.X + r, center.Y + r,   center.Z);
				Emit(center.X + r, center.Y - r,   center.Z);
				Emit(center.X - r, center.Y + r,   center.Z);
				Emit(center.X - r, center.Y - r,   center.Z);

				// Extra samples to make the ring feel “continuous”
				Emit(center.X + r, center.Y + (r > 1 ? 1 : 0), center.Z);
				Emit(center.X + r, center.Y - (r > 1 ? 1 : 0), center.Z);
				Emit(center.X - r, center.Y + (r > 1 ? 1 : 0), center.Z);
				Emit(center.X - r, center.Y - (r > 1 ? 1 : 0), center.Z);
				Emit(center.X + (r > 1 ? 1 : 0), center.Y + r, center.Z);
				Emit(center.X - (r > 1 ? 1 : 0), center.Y + r, center.Z);
				Emit(center.X + (r > 1 ? 1 : 0), center.Y - r, center.Z);
				Emit(center.X - (r > 1 ? 1 : 0), center.Y - r, center.Z);
			}

			void Emit(int x, int y, int z)
			{
				Point3D loc = new Point3D(x, y, z);
				Effects.SendLocationEffect(loc, map, waveID, 30, 10, ExplosionHue, 0);
			}
		}


		private void Detonate_OnTick(object state)
		{
			if (Deleted)
			{
				return;
			}

			var states = (object[])state;
			Mobile from = (Mobile)states[0];
			int timer = (int)states[1];

			object parent = FindParent(from);

			if (timer == 0)
			{
				Point3D loc;
				Map map;

				if (parent is Item)
				{
					Item item = (Item)parent;

					loc = item.GetWorldLocation();
					map = item.Map;
				}
				else if (parent is Mobile)
				{
					Mobile m = (Mobile)parent;

					loc = m.Location;
					map = m.Map;
				}
				else
				{
					return;
				}

				Explode(from, true, loc, map);
				m_Timer = null;
			}
			else
			{
				if (parent is Item)
				{
					((Item)parent).PublicOverheadMessage(MessageType.Regular, 0x22, false, timer.ToString());
				}
				else if (parent is Mobile)
				{
					((Mobile)parent).PublicOverheadMessage(MessageType.Regular, 0x22, false, timer.ToString());
				}

				states[1] = timer - 1;
			}
		}

		private void Reposition_OnTick(object state)
		{
			if (Deleted)
			{
				return;
			}

			var states = (object[])state;
			Mobile from = (Mobile)states[0];
			IPoint3D p = (IPoint3D)states[1];
			Map map = (Map)states[2];

			Point3D loc = new Point3D(p);

			if (InstantExplosion)
			{
				Explode(from, true, loc, map);
			}
			else
			{
				MoveToWorld(loc, map);
			}
		}

		private class ThrowTarget : Target
		{
			private readonly BaseMegaBomb m_Potion;

			public ThrowTarget(BaseMegaBomb potion)
				: base(12, true, TargetFlags.None)
			{
				m_Potion = potion;
			}

			public BaseMegaBomb Potion { get { return m_Potion; } }

			protected override void OnTarget(Mobile from, object targeted)
			{
				if (m_Potion.Deleted || m_Potion.Map == Map.Internal)
					return;

				IPoint3D p = targeted as IPoint3D;
				if (p == null)
					return;

				Map map = from.Map;
				if (map == null)
					return;

				SpellHelper.GetSurfaceTop(ref p);
				from.RevealingAction();

				IEntity to = new Entity(Serial.Zero, new Point3D(p), map);

				if (p is Mobile)
				{
					if (!RelativeLocation) // explosion location = current mob location
					{
						p = ((Mobile)p).Location;
					}
					else
					{
						to = (Mobile)p;
					}
				}

				// Moving projectile – pass the item hue
				// choose a projectile visual (bottle vs hueable FX), then apply config hue
				int projId = Server.Custom.Engineering.Grenadier.GrenadierConfig.UseHueableProjectile
					? Server.Custom.Engineering.Grenadier.GrenadierConfig.ProjectileFxItemID
					: m_Potion.ItemID;

				Effects.SendMovingEffect(from, to, projId, 7, 0, false, false, m_Potion.ExplosionHue, 0);

				if (m_Potion.Amount > 1)
					Mobile.LiftItemDupe(m_Potion, 1);

				m_Potion.Internalize();

				Timer.DelayCall(
					TimeSpan.FromSeconds(1.0),
					new TimerStateCallback(m_Potion.Reposition_OnTick),
					new object[] { from, p, map });
			}
		}

	}
}
