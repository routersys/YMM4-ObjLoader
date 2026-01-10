using System.Windows.Media.Media3D;

namespace ObjLoader.Services
{
    public interface ICameraManipulator
    {
        double CamX { get; set; }
        double CamY { get; set; }
        double CamZ { get; set; }
        double TargetX { get; set; }
        double TargetY { get; set; }
        double TargetZ { get; set; }
        double ViewCenterX { get; set; }
        double ViewCenterY { get; set; }
        double ViewCenterZ { get; set; }
        double ViewRadius { get; set; }
        double ViewTheta { get; set; }
        double ViewPhi { get; set; }

        PerspectiveCamera Camera { get; }
        double ModelHeight { get; }
        int ViewportHeight { get; }
        bool IsSnapping { get; }
        bool IsTargetFixed { get; set; }

        void UpdateVisuals();
        void SyncToParameter();
        void RecordUndo();
        void AnimateView(double theta, double phi);
    }
}