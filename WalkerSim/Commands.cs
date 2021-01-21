using System;
using System.Collections.Generic;

namespace WalkerSim
{
    public class CommandWalkerSim : ConsoleCmdAbstract
    {
        public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
        {
            try
            {
                if (_params.Count < 1)
                    return;

                var sim = API._sim;
                if (sim == null)
                    return;

                switch (_params[0])
                {
                    case "reset":
                        sim.Reset();
                        break;
                    case "timescale":
                        if (_params.Count < 2)
                            Log.Out("Missing parameter: <scale>");
                        else
                        {
                            float scale = 1.0f;
                            if (float.TryParse(_params[1], out scale))
                            {
                                sim.SetTimeScale(scale);
                            }
                        }
                        break;
                    case "walkspeedscale":
                        if (_params.Count < 2)
                            Log.Out("Missing parameter: <walkspeedscale>");
                        else
                        {
                            float scale = 1.0f;
                            if (float.TryParse(_params[1], out scale))
                            {
                                sim.SetWalkSpeedScale(scale);
                            }
                        }
                        break;
                }
            }
            catch (Exception e)
            {
                Log.Out("Exception: {0}", e.Message);
            }
        }

        public override string[] GetCommands()
        {
            return new string[] { "walkersim" };
        }

        public override string GetDescription()
        {
            return "Walker Sim";
        }

        public override string GetHelp()
        {
            return "walkersim <params>";
        }
    }
}
