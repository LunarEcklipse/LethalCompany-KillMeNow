using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Permissions;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Microsoft.CodeAnalysis;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;

namespace LC_Killbind_Mod
{
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    public class KillMeNow : BaseUnityPlugin
    {
        public const string pluginGuid = "lunarecklipse.lethalcompany.killmenow";
        public const string pluginName = "Kill Me Now";
        public const string pluginVersion = "1.0.0";

        public void Awake()
        {
            Logger.LogInfo("Kill Me Now is active!");

            Harmony harmony = new Harmony(pluginGuid);

            MethodInfo original = AccessTools.Method(typeof(HUDManager), "SubmitChat_performed");
            MethodInfo patch = AccessTools.Method(typeof(HUDManager_KMNPatch), "SubmitChat_performed_KMN");

            harmony.Patch(original, new HarmonyMethod(patch));
            Logger.LogInfo("Kill Me Now should have patched every method.");
        }
    }

    /* == HOW IT WORKS ==
     * 
     * We start by typing /kill and adding a space.
     * Position 1 is the death animation
     * Position 2 is the cause of death
     * Position 3 is whether or not we want to leave a corpse ("true"/"false")
     * 
     * 1) We make the string lowercase first.
     * 2) We split the string into an array of strings separated by spaces.
     * 3) We remove any strings that are empty from the array
     * 4) We branch behavior based on the length of the array.
     * 5) If length of array is 1, we just do a basic kill
     * 6) If length of array is 2, we kill them with the specified death animation if it exists.
     * 7) If length of array is 3, we kill them with the specified death animation if it exists, and make the cause of death what they specified if it exists.
     * 8) If length of array is 4, we kill them with the specified death animation if it exists, make the cause of death what they specified if it exists, and remove the body if specified.
     */

    public static class KMN_Helper
    {
        public static int getDeathAnimation(string daString) // Return a numeric cause of death from the provided string.
        {
            /* == Known Death Anims ==
             * 0 = Generic Death Anim
             * 1 = Ghost Girl Death Anim
             * 2 = Coilhead Death Anim
             */
            if (daString.IsNullOrWhiteSpace()) { return 0; }
            daString = daString.ToLower(); // Just in case we perform this here.
            switch (daString)
            {
                case "ghost":
                case "girl":
                case "dress":
                case "ghostgirl":
                case "haunt":
                case "haunted":
                case "haunting":
                    return 1;
                case "coil":
                case "coilhead":
                case "coil-head":
                case "spring":
                case "springhead":
                case "spring-head":
                case "springman":
                case "spring-man":
                case "coiled":
                    return 2;
                default:
                    return 0;
            }
        }

        public static CauseOfDeath getDeathCauseFromString(string codString) // Return a CauseOfDeath based on the input. If input is only x length, we pass the deathanim in here and use that result.
        {
            if (codString.IsNullOrWhiteSpace()) { return CauseOfDeath.Unknown; }
            codString = codString.ToLower(); // Just in case we perform this here.
            switch (codString)
            {
                case "ghost":
                case "girl":
                case "dress":
                case "ghostgirl":
                case "haunt":
                case "haunted":
                case "haunting":
                case "unknown":
                    return CauseOfDeath.Unknown;
                case "fall":
                case "fell":
                case "falling":
                case "falled":
                case "gravity":
                    return CauseOfDeath.Gravity;
                case "landmine":
                case "mine":
                case "bomb":
                case "explosion":
                case "explode":
                case "exploded":
                case "blast":
                    return CauseOfDeath.Blast;
                case "coil":
                case "coilhead":
                case "coil-head":
                case "spring":
                case "springhead":
                case "spring-head":
                case "springman":
                case "spring-man":
                case "coiled":
                case "thumper":
                case "thumped":
                case "shark":
                case "landshark":
                case "land-shark":
                case "crawler":
                case "hoarderbug":
                case "hoarder-bug":
                case "hoarder":
                case "hoarding":
                case "bug":
                case "spider":
                case "bunkerspider":
                case "bunker-spider":
                case "bunker":
                case "dog":
                case "eyeless":
                case "eyeless-dog":
                case "dogs":
                case "bitten":
                case "bit":
                case "blind-dog":
                case "jester":
                case "mauled":
                case "mauling":
                case "maul":
                    return CauseOfDeath.Mauling;
                case "turret":
                case "gun":
                case "shot":
                case "nutcracker":
                case "shotgun":
                case "smg":
                case "gunshot":
                case "gunshots":
                    return CauseOfDeath.Gunshots;
                case "kicking":
                case "kick":
                case "kicked":
                    return CauseOfDeath.Kicking;
                case "choke":
                case "choked":
                case "mask":
                case "comedy":
                case "tragedy":
                case "masked":
                case "lasso":
                case "lassoman":
                case "lasso-man":
                case "sand":
                case "mud":
                case "quicksand":
                case "quick-sand":
                case "snare":
                case "flea":
                case "snare-flea":
                case "snareflea":
                case "centipede":
                case "suffocate":
                case "suffocation":
                case "suffocated":
                    return CauseOfDeath.Suffocation;
                case "braken":
                case "bracken":
                case "flowerman":
                case "flower-man":
                case "strangle":
                case "strangled":
                case "strangulation":
                    return CauseOfDeath.Strangulation;
                case "giant":
                case "forest":
                case "forest-giant":
                case "forest-guardian":
                case "guardian":
                case "crush":
                case "crushed":
                case "crushing":
                    return CauseOfDeath.Crushing;
                case "water":
                case "drowning":
                case "drowned":
                    return CauseOfDeath.Drowning;
                case "bees":
                case "circuitbees":
                case "circuit-bees":
                case "circuit":
                case "electrocution":
                case "electricity":
                case "electrified":
                case "electrocute":
                case "zapped":
                    return CauseOfDeath.Electrocution;
                default:
                    return CauseOfDeath.Unknown;
            }
        }

        public static bool shouldSpawnBodyFromString(string boolString) // Determines whether or not the body should be deleted upon death. We also pass in causes of death that don't result in a body like giants and worms.
        {
            if (boolString.IsNullOrWhiteSpace()) { return true; }
            boolString = boolString.ToLower(); // Just in case we perform this here.
            switch (boolString)
            {
                case "giant":
                case "forest":
                case "forest-giant":
                case "forest-guardian":
                case "guardian":
                case "worm":
                case "sandworm":
                case "sand-worm":
                case "earth":
                case "leviathan":
                case "earth-leviathan":
                case "no":
                case "false":
                case "f":
                case "n":
                    return false;
                default:
                    return true;
            }
        }
    }

    public class HUDManager_KMNPatch
    {
        [HarmonyPatch(typeof(HUDManager), "SubmitChat_performed")]
        [HarmonyPrefix]
        public static bool SubmitChat_performed_KMN(HUDManager __instance)
        {
            System.Console.WriteLine("KillMeNow patch function executed.");
            string inputText = __instance.chatTextField.text;
            if (inputText.StartsWith("/kill")) // The 
            {
                System.Console.WriteLine("Attempting to kill player.");
                inputText = inputText.ToLower();
                string[] inputArr = inputText.Split(' ');
                inputArr = inputArr.Where(x => !string.IsNullOrEmpty(x.Trim())).ToArray();

                int playerDeathAnim = 0;
                CauseOfDeath playerCauseOfDeath = CauseOfDeath.Unknown;
                bool playerSpawnBody = true;

                switch (inputArr.Length)
                {
                    case 0: // What?
                        break;
                    case 1:
                        break;
                    case 2:
                        playerDeathAnim = KMN_Helper.getDeathAnimation(inputArr[1]);
                        playerCauseOfDeath = KMN_Helper.getDeathCauseFromString(inputArr[1]);
                        playerSpawnBody = KMN_Helper.shouldSpawnBodyFromString(inputArr[1]);
                        break;
                    case 3:
                        playerDeathAnim = KMN_Helper.getDeathAnimation(inputArr[1]);
                        playerCauseOfDeath = KMN_Helper.getDeathCauseFromString(inputArr[2]);
                        playerSpawnBody = KMN_Helper.shouldSpawnBodyFromString(inputArr[2]);
                        break;
                    default: // Should only trigger on 4 or more. If this is somehow not the case than we have much bigger problems because arrays returning negative values are... Something else.
                        if (inputArr.Length >= 4) // Just in case of some really wild edge case.
                        {
                            playerDeathAnim = KMN_Helper.getDeathAnimation(inputArr[1]);
                            playerCauseOfDeath = KMN_Helper.getDeathCauseFromString(inputArr[2]);
                            playerSpawnBody = KMN_Helper.shouldSpawnBodyFromString(inputArr[3]);
                        }
                        break;
                }
                System.Console.WriteLine("playerDeathAnim=" + playerDeathAnim.ToString());
                System.Console.WriteLine("playerCauseOfDeath=" + playerCauseOfDeath.ToString());
                System.Console.WriteLine("playerSpawnBody=" + playerSpawnBody.ToString());
                __instance.localPlayer.KillPlayer(default(Vector3), playerSpawnBody, playerCauseOfDeath, playerDeathAnim);
                System.Console.WriteLine("Player has been killed!");
                __instance.localPlayer = GameNetworkManager.Instance.localPlayerController;
                __instance.localPlayer.isTypingChat = false;
                __instance.chatTextField.text = "";
                ((Behaviour)(object)__instance.typingIndicator).enabled = false;
                return false;
            }
            return true;
        }
    }

}
