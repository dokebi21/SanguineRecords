﻿using Bloodstone.API;
using ProjectM;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System;

namespace SanguineArchives.VBloodArchives.Services
{
    internal class KillVBloodService
    {
        public Dictionary<string, HashSet<string>> vbloodKills = new();
        private PrefabCollectionSystem _prefabCollectionSystem = Core.PrefabCollectionSystem;
        public Dictionary<string, DateTime> lastKillerUpdate = new();
        public Dictionary<string, float> combatDuration = new();
        public Dictionary<string, Dictionary<string, float>> maxPlayerLevels = new();
        public Dictionary<string, bool> startedWhileRecovering = new();

        public void SetCombatDuration(string vblood, float duration)
        {
            combatDuration[vblood] = duration;
        }

        public void SetMaxPlayerLevels(string vblood, Dictionary<string, float> playerLevels)
        {
            maxPlayerLevels[vblood] = playerLevels;
        }

        public void ResetStartedWhileRecovering(string vblood)
        {
            startedWhileRecovering.Remove(vblood);
        }

        public void AddKiller(string vblood, string killerCharacterName)
        {
            if (!vbloodKills.ContainsKey(vblood))
            {
                vbloodKills[vblood] = new HashSet<string>();
            }
            vbloodKills[vblood].Add(killerCharacterName);
        }

        public void RemoveKillers(string vblood)
        {
            vbloodKills[vblood] = new HashSet<string>();
            SetCombatDuration(vblood, 0);
            maxPlayerLevels.Remove(vblood);
            startedWhileRecovering.Remove(vblood);
        }

        public List<string> GetKillers(string vblood)
        {
            return vbloodKills[vblood].ToList();
        }

        /**
         * Player levels must not be above V Blood level during the fight.
         */
        public bool CheckPlayerLevelsDuringCombat(string vblood)
        {
            var vbloodLevel = 0;
            if (Core.Prefabs.TryGetItem(vblood, out var prefabGuid))
            {
                if (Core.PrefabCollectionSystem._PrefabLookupMap.TryGetValue(prefabGuid, out var prefabEntity))
                {
                    vbloodLevel = prefabEntity.Read<UnitLevel>().Level;
                }
            }

            float maxLevel = -1;
            foreach (var (characterName, playerLevel) in maxPlayerLevels[vblood])
            {
                maxLevel = Math.Max(maxLevel, playerLevel);
            }
            return maxLevel <= vbloodLevel;
        }

        public bool CheckStartedWhileRecovering(string vblood)
        {
            Core.Log.LogInfo($"CheckStartedWhileRecovering: {startedWhileRecovering.ContainsKey(vblood)} vs {startedWhileRecovering.ContainsKey(vblood) && startedWhileRecovering[vblood]}");
            return startedWhileRecovering.ContainsKey(vblood) && startedWhileRecovering[vblood];
        }

        public void AnnounceVBloodKill(string vblood)
        {
            var killers = GetKillers(vblood);

            if (killers.Count == 0)
            {
                RemoveKillers(vblood);
                return;
            }

            combatDuration.TryGetValue(vblood, out var combatDurationSeconds);
            var killersLabel = ChatColor.Yellow(CombinedKillersLabel(vblood));
            if (!Core.Prefabs.TryGetItem(vblood, out var vbloodPrefab)) return;
            var vbloodLabel = ChatColor.Purple(vbloodPrefab.GetLocalizedName());
            // var basePrefixLabel = ChatColor.Green("Congratulations!");
            // var baseSuffixLabel = $"{vbloodLabel} was defeated by {killersLabel} in {combatDurationSeconds:F2} seconds!";
            var defaultCongratsMessage =
                ChatColor.Green(
                    $"Congratulations to {killersLabel} for defeating {vbloodLabel} in {combatDurationSeconds:F2} seconds!");
            Core.Log.LogInfo($"AnnounceVBloodKill: VBlood {vblood} was killed by {killersLabel}!");

            if (killers.Count == 1 && maxPlayerLevels[vblood].Count == 1)
            {
                var newRecord = new VBloodRecord
                {
                    CharacterName = killers[0],
                    DateTime = DateTime.Now.ToString("o"),
                    CombatDuration = combatDurationSeconds,
                };

                if (newRecord.CombatDuration == 0)
                {
                    SendVBloodMessageToAll(defaultCongratsMessage);
                    RemoveKillers(vblood);
                    return;
                }

                if (!CheckPlayerLevelsDuringCombat(vblood) || CheckStartedWhileRecovering(vblood))
                {
                    SendVBloodMessageToAll(defaultCongratsMessage);
                    RemoveKillers(vblood);
                    return;
                }

                if (Core.VBloodRecordsService.IsNewTopRecord(vblood, newRecord))
                {
                    SendVBloodMessageToAll(defaultCongratsMessage);
                    var newTopRecordLabel = ChatColor.Green($"** A new top record has been set! **");
                    SendVBloodMessageToAll(newTopRecordLabel);
                    if (Core.VBloodRecordsService.TryGetTopRecordForVBlood(vblood, out VBloodRecord topRecord))
                    {
                        var difference = Core.VBloodRecordsService.GetCurrentTopRecord(vblood) - combatDurationSeconds;
                        var differenceLabel = ChatColor.Green($"{difference:F2}");
                        var topRecordNoticeLabel = $"The new record is faster by {differenceLabel} seconds.";
                        SendVBloodMessageToAll(topRecordNoticeLabel);
                    }
                }
                else
                {
                    SendVBloodMessageToAll(defaultCongratsMessage);
                }

                if (Core.VBloodRecordsService.IsNewPlayerRecord(vblood, newRecord))
                {
                    var personalRecordPrefixLabel = ChatColor.Green("** A new personal record has been set! **");
                    SendVBloodMessageToPlayers(killers, personalRecordPrefixLabel);
                    if (Core.VBloodRecordsService.TryGetPlayerRecordForVBlood(vblood, newRecord.CharacterName,
                            out var currentPlayerRecord))
                    {
                        var difference = Core.VBloodRecordsService.GetCurrentPlayerRecord(vblood, newRecord.CharacterName) - combatDurationSeconds;
                        var differenceLabel = ChatColor.Green($"{difference:F2}");
                        var personalRecordSuffixLabel = $"Your new record is faster by {differenceLabel} seconds.";
                        SendVBloodMessageToPlayers(killers, personalRecordSuffixLabel);
                    }
                }
                Core.VBloodRecordsService.AddRecord(vblood, newRecord);
            }
            if (killers.Count > 1 || maxPlayerLevels[vblood].Count > 1)
            {
                // More than 1 killer or fighter.
                SendVBloodMessageToAll(defaultCongratsMessage);
            }
            RemoveKillers(vblood);
        }

        /**
         * Send message to all users who didn't turn off VBlood notifications.
         */
        public void SendVBloodMessageToAll(string message)
        {
            var usersOnline = PlayerService.GetUsersOnline();
            foreach (var user in usersOnline)
            {
                Player player = new(user);
                ServerChatUtils.SendSystemMessageToClient(VWorld.Server.EntityManager, player.User.Read<User>(), ChatColor.Gray(message));
            }
        }

        /**
         * Send message to killers who didn't turn off VBlood notifications.
         */
        public void SendVBloodMessageToPlayers(List<string> players, string message)
        {
            var usersOnline = PlayerService.GetUsersOnline();
            foreach (var user in usersOnline)
            {
                Player player = new(user);
                if (players.Contains(player.Name))
                {
                    ServerChatUtils.SendSystemMessageToClient(VWorld.Server.EntityManager, player.User.Read<User>(), ChatColor.Gray(message));
                }
            }
        }

        private string CombinedKillersLabel(string vblood)
        {
            var killers = GetKillers(vblood);
            var sbKillersLabel = new StringBuilder();
            if (killers.Count == 0) return null;
            if (killers.Count == 1)
            {
                sbKillersLabel.Append(ChatColor.Yellow(killers[0]));
            }
            if (killers.Count == 2)
            {
                sbKillersLabel.Append($"{ChatColor.Yellow(killers[0])} and {ChatColor.Yellow(killers[1])}");
            }
            if (killers.Count > 2)
            {
                for (int i = 0; i < killers.Count; i++)
                {
                    if (i == killers.Count - 1)
                    {
                        sbKillersLabel.Append($"and {ChatColor.Yellow(killers[i])}");
                    }
                    else
                    {
                        sbKillersLabel.Append($"{ChatColor.Yellow(killers[i])}, ");
                    }
                }
            }
            return sbKillersLabel.ToString();
        }
    }
}
