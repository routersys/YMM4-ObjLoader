using ObjLoader.Rendering.Core;

namespace ObjLoader.Rendering.Models
{
    internal sealed class FrameState
    {
        public long Frame { get; private set; }
        public double CamX { get; private set; }
        public double CamY { get; private set; }
        public double CamZ { get; private set; }
        public double TargetX { get; private set; }
        public double TargetY { get; private set; }
        public double TargetZ { get; private set; }
        public int ActiveWorldId { get; private set; }
        public Dictionary<string, LayerState> LayerStates { get; } = new Dictionary<string, LayerState>();

        public void Update(long frame, double camX, double camY, double camZ, double targetX, double targetY, double targetZ, int activeWorldId, Dictionary<string, LayerState> layerStates)
        {
            Frame = frame;
            CamX = camX;
            CamY = camY;
            CamZ = camZ;
            TargetX = targetX;
            TargetY = targetY;
            TargetZ = targetZ;
            ActiveWorldId = activeWorldId;

            LayerStates.Clear();
            foreach (var kvp in layerStates)
            {
                LayerStates[kvp.Key] = kvp.Value;
            }
        }
    }
}