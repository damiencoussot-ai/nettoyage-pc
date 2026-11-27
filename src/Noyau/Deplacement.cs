// Velox - « tes propres pas » : savoir si le joueur se déplace (touches de déplacement ou stick gauche de la manette).
// En stéréo, un pas au centre peut être le tien ou celui d'un ennemi juste devant ou derrière toi : le son seul ne les
// distingue pas (même place, même forme). La seule différence sûre : tes pas n'existent que pendant que tu bouges. Le
// moteur « Pas boostés » remonte donc moins un son bref au centre pendant que tu te déplaces, et en plein dès que tu
// t'arrêtes (c'est aussi le réflexe du joueur : s'arrêter pour écouter).
//
// Lecture seule de l'état des touches et de la manette (GetAsyncKeyState, XInputGetState), comme le font les logiciels
// de micro « appuyer pour parler » : rien n'est injecté dans le jeu, aucune touche n'est interceptée ni enregistrée.
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace NettoyagePC
{
    internal static class DeplacementJoueur
    {
        /// <summary>Touches de déplacement : ZQSD (AZERTY), WASD (QWERTY), flèches.</summary>
        internal static readonly int[] Touches = { 'Z', 'Q', 'S', 'D', 'W', 'A', 0x25, 0x26, 0x27, 0x28 };
        /// <summary>Tes pas continuent un peu après avoir lâché la touche (le personnage freine) : « bouge » tenu 350 ms.</summary>
        internal const int TenueMs = 350;
        /// <summary>Stick gauche : au-delà de cette valeur (sur 32 767), tu marches (zone morte de Windows : 7 849).</summary>
        internal const int ZoneMorteStick = 9000;

        private static int dernierMouvement = Environment.TickCount - 100000;
        private static int demarre;

        /// <summary>Vrai si le joueur s'est déplacé dans les 350 dernières ms (faux hors Windows ou si la lecture échoue).</summary>
        public static bool Bouge { get { return unchecked(Environment.TickCount - Volatile.Read(ref dernierMouvement)) < TenueMs; } }

        /// <summary>Une touche de déplacement enfoncée, ou le stick gauche poussé au-delà de la zone morte.</summary>
        internal static bool EnDeplacement(Func<int, bool> toucheEnfoncee, int sticks, short lx, short ly)
        {
            if (toucheEnfoncee != null) foreach (int vk in Touches) if (toucheEnfoncee(vk)) return true;
            return sticks > 0 && (double)lx * lx + (double)ly * ly > (double)ZoneMorteStick * ZoneMorteStick;
        }

        /// <summary>Lance le fil qui relève l'état toutes les 15 ms (une seule fois ; s'arrête avec « fini »).</summary>
        public static void Demarrer(ManualResetEventSlim fini)
        {
            if (!OperatingSystem.IsWindows() || Interlocked.Exchange(ref demarre, 1) == 1) return;
            var fil = new Thread(() =>
            {
                bool clavier = true; int manette = 0;   // 0 : xinput1_4, 1 : xinput9_1_0, 2 : aucune
                long prochaineRecherche = 0; var absentes = new bool[4];
                while (fini == null || !fini.IsSet)
                {
                    try
                    {
                        bool bouge = false;
                        if (clavier)
                        {
                            try { bouge = EnDeplacement(vk => (GetAsyncKeyState(vk) & 0x8000) != 0, 0, 0, 0); }
                            catch { clavier = false; }
                        }
                        if (!bouge && manette < 2)
                        {
                            // une manette absente est recherchée toutes les 2 s seulement (la demande peut prendre du temps)
                            bool chercher = Environment.TickCount64 >= prochaineRecherche;
                            if (chercher) prochaineRecherche = Environment.TickCount64 + 2000;
                            for (uint i = 0; i < 4 && !bouge; i++)
                            {
                                if (absentes[i] && !chercher) continue;
                                XINPUT_STATE s; uint r;
                                try { r = manette == 0 ? XInputGetState14(i, out s) : XInputGetState910(i, out s); }
                                catch (DllNotFoundException) { manette++; break; }
                                catch (EntryPointNotFoundException) { manette++; break; }
                                absentes[i] = r != 0;
                                if (r == 0 && EnDeplacement(null, 1, s.Gamepad.sThumbLX, s.Gamepad.sThumbLY)) bouge = true;
                            }
                        }
                        if (bouge) Volatile.Write(ref dernierMouvement, Environment.TickCount);
                    }
                    catch { }
                    if (fini != null) fini.Wait(15); else Thread.Sleep(15);
                }
                Interlocked.Exchange(ref demarre, 0);
            }) { IsBackground = true, Priority = ThreadPriority.BelowNormal, Name = "Velox tes déplacements" };
            fil.Start();
        }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vk);

        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_GAMEPAD { public ushort wButtons; public byte bLeftTrigger, bRightTrigger; public short sThumbLX, sThumbLY, sThumbRX, sThumbRY; }
        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_STATE { public uint dwPacketNumber; public XINPUT_GAMEPAD Gamepad; }
        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
        private static extern uint XInputGetState14(uint index, out XINPUT_STATE etat);
        [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")]
        private static extern uint XInputGetState910(uint index, out XINPUT_STATE etat);
    }
}
