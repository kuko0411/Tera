﻿using System;
using System.Collections.Generic;
using System.Linq;
using Tera.Game.Messages;

namespace Tera.Game
{
    public class AbnormalityTracker
    {
        private readonly Dictionary<EntityId, List<Abnormality>> _abnormalities =
            new Dictionary<EntityId, List<Abnormality>>();
        public Action<SkillResult> UpdateDamageTracker;
        internal EntityTracker EntityTracker;
        internal PlayerTracker PlayerTracker;
        internal HotDotDatabase HotDotDatabase;
        internal AbnormalityStorage AbnormalityStorage;

        public AbnormalityTracker(EntityTracker entityTracker, PlayerTracker playerTracker, HotDotDatabase hotDotDatabase, AbnormalityStorage abnormalityStorage, Action<SkillResult> update=null)
        {
            EntityTracker = entityTracker;
            PlayerTracker = playerTracker;
            HotDotDatabase = hotDotDatabase;
            UpdateDamageTracker = update;
            AbnormalityStorage = abnormalityStorage;
        }

        public void RegisterNpcStatus(SNpcStatus NpcStatus)
        {
            RegisterAggro(NpcStatus);
            if (NpcStatus.Enraged)
                AddAbnormality(NpcStatus.Npc, NpcStatus.Target,0,0,8888888,NpcStatus.Time.Ticks);
            else
                DeleteAbnormality(NpcStatus);
        }

        public void RegisterSlaying(UserEntity user, bool Slaying, long ticks)
        {
            if (user == null) return;
            if (Slaying)
                AddAbnormality(user.Id, user.Id, 0, 0, 8888889, ticks);
            else
                DeleteAbnormality(user.Id, 8888889, ticks);
        }
        public void RegisterDead(SCreatureLife dead)
        {
            var user = EntityTracker.GetOrNull(dead.User) as UserEntity;
            if (user == null) return;
            var player = PlayerTracker.GetOrUpdate(user);
            var time = dead.Time.Ticks / TimeSpan.TicksPerSecond;
            if (dead.Dead)
                AbnormalityStorage.Death(player).Start(time);
            else
                AbnormalityStorage.Death(player).End(time);
        }
        private void RegisterAggro(SNpcStatus aggro)
        {
            var time = aggro.Time.Ticks / TimeSpan.TicksPerSecond;
            var entity = EntityTracker.GetOrNull(aggro.Npc) as NpcEntity;
            if (entity == null) return;//not sure why, but sometimes it fails
            var user = EntityTracker.GetOrNull(aggro.Target) as UserEntity;
            if (user != null)
            {
                var player = PlayerTracker.GetOrUpdate(user);
                if (AbnormalityStorage.Last(entity) != player)
                {
                    if (AbnormalityStorage.Last(entity) != null)
                        AbnormalityStorage.AggroEnd(AbnormalityStorage.Last(entity), entity, time);
                    AbnormalityStorage.AggroStart(player, entity, time);
                    AbnormalityStorage.LastAggro[entity] = player;
                }
            }
            else
            {
                if (AbnormalityStorage.Last(entity) != null)
                {
                    AbnormalityStorage.AggroEnd(AbnormalityStorage.Last(entity), entity, time);
                    AbnormalityStorage.LastAggro[entity] = null;
                }
            }
        }

        public void StopAggro(SDespawnNpc aggro)
        {
            var time = aggro.Time.Ticks / TimeSpan.TicksPerSecond;
            var entity = EntityTracker.GetOrNull(aggro.Npc) as NpcEntity;
            if (entity == null) return;// Strange, but seems there are not only NPC or something wrong with trackers
            if (AbnormalityStorage.Last(entity) != null)
            {
                AbnormalityStorage.AggroEnd(AbnormalityStorage.Last(entity), entity, time);
                AbnormalityStorage.LastAggro[entity] = null;
            }
        }

        public void AddAbnormality(SAbnormalityBegin message)
        {
            AddAbnormality(message.TargetId, message.SourceId, message.Duration, message.Stack, message.AbnormalityId,
                message.Time.Ticks);
        }

        public void AddAbnormality(EntityId target, EntityId source, int duration, int stack, int abnormalityId,
            long ticks)
        {
            if (!_abnormalities.ContainsKey(target))
            {
                _abnormalities.Add(target, new List<Abnormality>());
            }
            var hotdot = HotDotDatabase.Get(abnormalityId);
            if (hotdot == null)
            {
                return;
            }

            if (_abnormalities[target].Where(x => x.HotDot.Id == abnormalityId).Count() == 0) //dont add existing abnormalities since we don't delete them all, that may cause many untrackable issues.
                _abnormalities[target].Add(new Abnormality(hotdot, source, target, duration, stack, ticks, this));

        }

        public void RefreshAbnormality(SAbnormalityRefresh message)
        {
            if (!_abnormalities.ContainsKey(message.TargetId))
            {
                return;
            }
            var abnormalityUser = _abnormalities[message.TargetId];
            foreach (var abnormality in abnormalityUser)
            {
                if (abnormality.HotDot.Id != message.AbnormalityId) continue;
                abnormality.Refresh(message.StackCounter, message.Duration, message.Time.Ticks);
                return;
            }
        }

        public bool AbnormalityExist(EntityId target, HotDot dot)
        {
            if (!_abnormalities.ContainsKey(target))
            {
                return false;
            }
            var abnormalityTarget = _abnormalities[target];
            for(var i = 0; i < abnormalityTarget.Count; i++)
            {
                if(abnormalityTarget[i].HotDot == dot)
                {
                    return true;
                }
            }
            return false;
        }

        public void DeleteAbnormality(EntityId target, int abnormalityId, long ticks)
        {
            if (!_abnormalities.ContainsKey(target))
            {
                return;
            }

            var abnormalityUser = _abnormalities[target];

            for (var i = 0; i < abnormalityUser.Count; i++)
            {
                if (abnormalityUser[i].HotDot.Id == abnormalityId)
                {
                    abnormalityUser[i].ApplyBuffDebuff(ticks);
                    abnormalityUser.Remove(abnormalityUser[i]);
                    break;
                }
            }

            if (abnormalityUser.Count == 0)
            {
                _abnormalities.Remove(target);
                return;
            }
            _abnormalities[target] = abnormalityUser;
        }

        public void DeleteAbnormality(SAbnormalityEnd message)
        {
            DeleteAbnormality(message.TargetId, message.AbnormalityId, message.Time.Ticks);
        }
        public void DeleteAbnormality(SDespawnNpc message)
        {
            DeleteAbnormality(message.Npc, message.Time.Ticks);
        }

        public void DeleteAbnormality(SNpcStatus message)
        {
            DeleteAbnormality(message.Npc, 8888888, message.Time.Ticks);
        }

        public void DeleteAbnormality(SCreatureChangeHp message)
        {
            DeleteAbnormality(message.TargetId, 8888889, message.Time.Ticks);
        }

        public void DeleteAbnormality(SDespawnUser message)
        {
            DeleteAbnormality(message.User, message.Time.Ticks);
        }

        private void DeleteAbnormality(EntityId entity, long ticks)
        {
            if (!_abnormalities.ContainsKey(entity))
            {
                return;
            }
            foreach (var abno in _abnormalities[entity])
            {
                abno.ApplyBuffDebuff(ticks);
            }
            _abnormalities.Remove(entity);
        }


        public void Update(SPlayerChangeMp message)
        {
            Update(message.TargetId, message.SourceId, message.MpChange, message.Type, message.Critical == 1, false,
                message.Time.Ticks);
        }

        private void Update(EntityId target, EntityId source, int change, int type, bool critical, bool isHp, long time)
        {
            if (!_abnormalities.ContainsKey(target))
            {
                return;
            }

            var abnormalities = _abnormalities[target];
            abnormalities = abnormalities.OrderByDescending(o => o.TimeBeforeApply).ToList();

            foreach (var abnormality in abnormalities)
            {
                if (abnormality.Source != source && abnormality.Source != abnormality.Target)
                {
                    continue;
                }

                if (isHp)
                {
                    if ((!(abnormality.HotDot.Hp > 0) || change <= 0) &&
                        (!(abnormality.HotDot.Hp < 0) || change >= 0)
                        ) continue;
                }
                else
                {
                    if ((!(abnormality.HotDot.Mp > 0) || change <= 0) &&
                        (!(abnormality.HotDot.Mp < 0) || change >= 0)
                        ) continue;
                }

                if ((int) HotDotDatabase.HotOrDot.Dot != type && (int) HotDotDatabase.HotOrDot.Hot != type)
                {
                    continue;
                }

                abnormality.Apply(change, critical, isHp, time);
                return;
            }
        }

        public void Update(SCreatureChangeHp message)
        {
            Update(message.TargetId, message.SourceId, message.HpChange, message.Type, message.Critical == 1, true, message.Time.Ticks);
            var user = EntityTracker.GetOrPlaceholder(message.TargetId) as UserEntity;
            RegisterSlaying(user, message.Slaying, message.Time.Ticks);
        }
    }
}