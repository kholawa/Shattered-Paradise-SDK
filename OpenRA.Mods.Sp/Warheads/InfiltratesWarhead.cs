﻿#region Copyright & License Information
/*
 * Copyright The OpenRA-SP Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using OpenRA.GameRules;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Warheads;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Sp.Warheads
{
	[Desc("Hack: This warhead only used for mission to trigger the lua trigger. It only calls victim's INotifyInfiltrated and doesn't directly affect the one who fire it")]
	sealed class InfiltratesWarhead : Warhead
	{
		[Desc("Infiltrate will be applied to actors in this area. A value of zero means only targeted actor will be damaged.")]
		public readonly WDist Spread = WDist.Zero;

		[Desc("The `TargetTypes` from `Targetable` that are allowed to enter.")]
		public readonly BitSet<TargetableType> Types = default;

		[NotificationReference("Speech")]
		[Desc("Notification to play when a target is infiltrated.")]
		public readonly string Notification = null;

		[TranslationReference(optional: true)]
		[Desc("Text notification to display when a target is infiltrated.")]
		public readonly string TextNotification = null;

		public override void DoImpact(in Target target, WarheadArgs args)
		{
			var firedBy = args.SourceActor;
			if (firedBy == null)
				return;

			if (Spread == WDist.Zero)
			{
				if (target.Type == TargetType.Actor)
				{
					var victim = target.Actor;

					if (!IsValidAgainst(victim, firedBy))
						return;

					// PERF: Avoid using TraitsImplementing<HitShape> that needs to find the actor in the trait dictionary.
					var closestActiveShape = (HitShape)victim.EnabledTargetablePositions.MinByOrDefault(t =>
					{
						if (t is HitShape h)
							return h.DistanceFromEdge(victim, victim.CenterPosition);
						else
							return WDist.MaxValue;
					});

					// Cannot be damaged without an active HitShape
					if (closestActiveShape == null)
						return;

					Infiltrate(victim, firedBy);
				}

				return;
			}

			var pos = target.CenterPosition;
			foreach (var victim in firedBy.World.FindActorsInCircle(pos, Spread))
			{
				if (!IsValidAgainst(victim, firedBy))
					continue;

				HitShape closestActiveShape = null;
				var closestDistance = int.MaxValue;

				// PERF: Avoid using TraitsImplementing<HitShape> that needs to find the actor in the trait dictionary.
				foreach (var targetPos in victim.EnabledTargetablePositions)
				{
					if (targetPos is HitShape hitshape)
					{
						var distance = hitshape.DistanceFromEdge(victim, pos).Length;
						if (distance < closestDistance)
						{
							closestDistance = distance;
							closestActiveShape = hitshape;
						}
					}
				}

				// Cannot be damaged without an active HitShape.
				if (closestActiveShape == null)
					continue;

				// Summary: when find victim actors, OpenRA ignores height,
				// but when calculate hitshape, most of damage warhead will
				// consider height.
				Infiltrate(victim, firedBy);
			}
		}

		void Infiltrate(Actor victim, Actor firedBy)
		{
			if (!Types.Overlaps(victim.GetEnabledTargetTypes()) || !ValidRelationships.HasRelationship(firedBy.Owner.RelationshipWith(victim.Owner)))
				return;

			foreach (var t in victim.TraitsImplementing<INotifyInfiltrated>())
				t.Infiltrated(victim, firedBy, Types);

			if (!string.IsNullOrEmpty(Notification))
				Game.Sound.PlayNotification(firedBy.World.Map.Rules, firedBy.Owner, "Speech",
					Notification, firedBy.Owner.Faction.InternalName);

			TextNotificationsManager.AddTransientLine(firedBy.Owner, TextNotification);
		}
	}
}
