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

        protected override void FrameAction()
        {
            // 1. Obtenemos el equipo de nuestro jugador local
            var localPlayerPawn = graphics.GameProcess.ModuleClient.Read<IntPtr>(Offsets.dwLocalPlayerPawn);
            if (localPlayerPawn == IntPtr.Zero) return;

            var localTeam = graphics.GameProcess.Process.Read<int>(localPlayerPawn + Offsets.m_iTeamNum);

            // 2. Leemos la Entity List
            var entityList = graphics.GameProcess.ModuleClient.Read<IntPtr>(Offsets.dwEntityList);
            if (entityList == IntPtr.Zero) return;

            IntPtr voteControllerPtr = IntPtr.Zero;

            // Iteramos la Entity List buscando la entidad del controlador de votos
            // Empezamos en 64 porque los jugadores ocupan los primeros 64 lugares
            for (int i = 64; i < 1024; i++)
            {
                var listEntry = graphics.GameProcess.Process.Read<IntPtr>(entityList + (8 * (i >> 9)) + 16);
                if (listEntry == IntPtr.Zero) continue;

                var entity = graphics.GameProcess.Process.Read<IntPtr>(listEntry + 120 * (i & 0x1FF));
                if (entity == IntPtr.Zero) continue;

                // Obtenemos el nombre de diseño de la entidad (designerName)
                var entityIdentity = graphics.GameProcess.Process.Read<IntPtr>(entity + 0x10);
                if (entityIdentity == IntPtr.Zero) continue;

                var designerNamePtr = graphics.GameProcess.Process.Read<IntPtr>(entityIdentity + 0x20);
                if (designerNamePtr == IntPtr.Zero) continue;
                
                // Leemos byte por byte hasta encontrar el terminador nulo (0x00)
                var sb = new StringBuilder();
                for (int j = 0; j < 32; j++) // Leemos hasta un máximo de 32 caracteres por seguridad
                {
                    // Leemos 1 byte en la posición actual
                    byte b = graphics.GameProcess.Process.Read<byte>(designerNamePtr + j);

                    // Si el byte es 0, significa que el string terminó
                    if (b == 0) break;

                    // Convertimos el byte a caracter y lo agregamos
                    sb.Append((char)b);
                }

                string name = sb.ToString();

                if (name == "vote_controller")
                {
                    voteControllerPtr = entity;
                    break;
                }
            }


            // Si encontramos la entidad, sacamos la data de la votación
            if (voteControllerPtr != IntPtr.Zero)
            {
                var activeIssue = graphics.GameProcess.Process.Read<int>(voteControllerPtr + Offsets.m_iActiveIssueIndex);
                Console.WriteLine(activeIssue);
                // -1 o 999 suele significar que no hay votación activa
                if (activeIssue != -1 && activeIssue != 999)
                {
                    _votingTeam = graphics.GameProcess.Process.Read<int>(voteControllerPtr + Offsets.m_iOnlyTeamToVote);

                    // Verificamos si la votación es en el otro equipo (O es una global: 0)
                    if (_votingTeam != localTeam && _votingTeam > 1)
                    {
                        _isVoting = true;
                        // Índice 0 son votos a favor, Índice 1 (offset + 4) son votos en contra
                        _yesVotes = graphics.GameProcess.Process.Read<int>(voteControllerPtr + Offsets.m_nVoteOptionCount);
                        _noVotes = graphics.GameProcess.Process.Read<int>(voteControllerPtr + Offsets.m_nVoteOptionCount + 4);

                        // Mapeo básico de los index de Issues en CS2
                        _voteType = activeIssue switch
                        {
                            0 => "Kick Player",
                            1 => "Change Map",
                            2 => "Surrender",
                            3 => "Timeout",
                            4 => "Scramble Teams",
                            _ => $"Unknown Issue ({activeIssue})"
                        };
                        return; // Terminamos el frame
                    }
                }
            }

            _isVoting = false; // Si no encontró nada, apagamos el cartel
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
