using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CS2Cheat.Graphics;
using CS2Cheat.Utils;
using SharpDX.Direct2D1;
using SharpDX.Direct3D9;

namespace CS2Cheat.Features
{
    internal class VoteTeller(Graphics.Graphics graphics) : ThreadedServiceBase
    {
        private static bool _isVoting;
        private static int _votingTeam;
        private static int _yesVotes;
        private static int _noVotes;
        private static string _voteType = string.Empty;

private static DateTime _lastPrintTime = DateTime.MinValue;

        protected override void FrameAction()
        {
            var localPlayerPawn = graphics.GameProcess.ModuleClient.Read<IntPtr>(Offsets.dwLocalPlayerPawn);
            if (localPlayerPawn == IntPtr.Zero) return;
            var localTeam = graphics.GameProcess.Process.Read<int>(localPlayerPawn + Offsets.m_iTeamNum);

            var entityList = graphics.GameProcess.ModuleClient.Read<IntPtr>(Offsets.dwEntityList);
            if (entityList == IntPtr.Zero) return;

            IntPtr voteControllerPtr = IntPtr.Zero;

            // Buscamos el vote_controller
            for (int i = 64; i < 8192; i++)
            {
                var listEntry = graphics.GameProcess.Process.Read<IntPtr>(entityList + (8 * (i >> 9)) + 16);
                if (listEntry == IntPtr.Zero) continue;

                var entity = graphics.GameProcess.Process.Read<IntPtr>(listEntry + 120 * (i & 0x1FF));
                if (entity == IntPtr.Zero) continue;

                var entityIdentity = graphics.GameProcess.Process.Read<IntPtr>(entity + 0x10);
                if (entityIdentity == IntPtr.Zero) continue;

                var designerNamePtr = graphics.GameProcess.Process.Read<IntPtr>(entityIdentity + 0x20);
                if (designerNamePtr == IntPtr.Zero) continue;

                var sb = new StringBuilder();
                for (int j = 0; j < 32; j++)
                {
                    byte b = graphics.GameProcess.Process.Read<byte>(designerNamePtr + j);
                    if (b == 0) break;
                    if (b >= 32 && b <= 126) sb.Append((char)b);
                }

                if (sb.ToString() == "vote_controller")
                {
                    voteControllerPtr = entity;
                    break;
                }
            }

            // Si lo encontramos, vamos a chusmear qué tiene adentro
            if (voteControllerPtr != IntPtr.Zero)
            {
                // Leemos crudo con los offsets de tu dump
                int activeIssue = graphics.GameProcess.Process.Read<int>(voteControllerPtr + 1552);
                int votingTeam = graphics.GameProcess.Process.Read<int>(voteControllerPtr + 1556);
                int yesVotes = graphics.GameProcess.Process.Read<int>(voteControllerPtr + 1560);
                int noVotes = graphics.GameProcess.Process.Read<int>(voteControllerPtr + 1564);

                // IMPRIMIMOS SOLO 1 VEZ POR SEGUNDO PARA NO SPAMEAR
                if ((DateTime.Now - _lastPrintTime).TotalSeconds >= 1)
                {
                    Console.WriteLine($"[VOTE DATA] Issue: {activeIssue} | Team: {votingTeam} | Yes: {yesVotes} | No: {noVotes}");
                    _lastPrintTime = DateTime.Now;
                }

                // Forzamos temporalmente a que DIBUJE SIEMPRE para ver qué pasa en pantalla
                _isVoting = true;
                _votingTeam = votingTeam;
                _yesVotes = yesVotes;
                _noVotes = noVotes;
                _voteType = $"Issue ID: {activeIssue}"; 
            }
            else
            {
                _isVoting = false;
            }
        }

        public static void Draw(Graphics.Graphics graphics)
        {
           // if (!_isVoting) return;

            string teamName = _votingTeam == 2 ? "TERRORISTS" : "COUNTER-TERRORISTS";
            var teamColor = _votingTeam == 2
                ? new SharpDX.Mathematics.Interop.RawColorBGRA(0, 69, 255, 255)   // Terrorist (OrangeRed)
                : new SharpDX.Mathematics.Interop.RawColorBGRA(255, 191, 0, 255);

            string text = $"Enemy Vote: {teamName}{Environment.NewLine}Voting for: {_voteType}{Environment.NewLine}Yes: {_yesVotes} | No: {_noVotes}";

            // 1. Creamos el rectángulo delimitador nosotros mismos (Left, Top, Right, Bottom)
            // Le damos suficiente espacio para que el texto entre perfecto.
            var rect = new SharpDX.Mathematics.Interop.RawRectangle(10, 350, 500, 600);

            // 2. Llamamos directamente a la sobrecarga compleja, pasándole el rectángulo
            // Cambié FontConsolas32 por FontAzonix64 para que se vea gigante y bien igual que el timer
            graphics.FontAzonix64.DrawText(
                default, // Usar null es mejor que default para evitar ambigüedades
                text,
                rect,
                FontDrawFlags.Left | FontDrawFlags.Top, // Acá controlamos el formato, no usamos NoClip
                teamColor
            );
        }
    }
}
