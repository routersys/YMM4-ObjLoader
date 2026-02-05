using System.Windows.Media.Media3D;
using System.Windows.Media;
using System.Windows;
using ObjLoader.Localization;

namespace ObjLoader.Services.Rendering
{
    internal static class GizmoBuilder
    {
        public static void BuildGizmos(
            MeshGeometry3D gizmoX, MeshGeometry3D gizmoY, MeshGeometry3D gizmoZ,
            MeshGeometry3D gizmoXY, MeshGeometry3D gizmoYZ, MeshGeometry3D gizmoZX,
            MeshGeometry3D camVisual, MeshGeometry3D targetVisual,
            Point3D camPos, Point3D targetPos,
            bool isTargetFixed, double modelScale, bool isInteracting, double fov, double aspectRatio,
            bool isPilotView)
        {
            gizmoX.Positions.Clear(); gizmoX.TriangleIndices.Clear();
            gizmoY.Positions.Clear(); gizmoY.TriangleIndices.Clear();
            gizmoZ.Positions.Clear(); gizmoZ.TriangleIndices.Clear();
            gizmoXY.Positions.Clear(); gizmoXY.TriangleIndices.Clear();
            gizmoYZ.Positions.Clear(); gizmoYZ.TriangleIndices.Clear();
            gizmoZX.Positions.Clear(); gizmoZX.TriangleIndices.Clear();
            camVisual.Positions.Clear(); camVisual.TriangleIndices.Clear();
            targetVisual.Positions.Clear(); targetVisual.TriangleIndices.Clear();

            if (isPilotView) return;

            double gScale = modelScale * 0.15;
            double gThick = gScale * 0.05;
            Point3D gPos = isTargetFixed ? camPos : targetPos;

            int sphereDiv = isInteracting ? 4 : 16;
            int coneSegs = isInteracting ? 6 : 12;

            AddArrow(gizmoX, gPos, new Vector3D(1, 0, 0), gScale, gThick, coneSegs);
            AddArrow(gizmoY, gPos, new Vector3D(0, 1, 0), gScale, gThick, coneSegs);
            var zDir = isTargetFixed ? new Vector3D(0, 0, -1) : new Vector3D(0, 0, 1);
            AddArrow(gizmoZ, gPos, zDir, gScale, gThick, coneSegs);

            double pOff = gScale * 0.3;
            double pSz = gScale * 0.2;
            AddQuad(gizmoXY, gPos + new Vector3D(pOff, pOff, 0), new Vector3D(0, 0, 1), pSz);
            AddQuad(gizmoYZ, gPos + new Vector3D(0, pOff, pOff), new Vector3D(1, 0, 0), pSz);
            AddQuad(gizmoZX, gPos + new Vector3D(pOff, 0, pOff), new Vector3D(0, 1, 0), pSz);

            var dir = targetPos - camPos;
            if (dir.LengthSquared < 0.0001) dir = new Vector3D(0, 0, -1);
            dir.Normalize();

            AddLineToMesh(camVisual, camPos, camPos + (dir * modelScale * 100.0), modelScale * 0.003 * 0.5);

            Vector3D forward = dir;
            Vector3D up = new Vector3D(0, 1, 0);
            Vector3D right = Vector3D.CrossProduct(forward, up);
            if (right.LengthSquared < 0.001) right = new Vector3D(1, 0, 0);
            right.Normalize();
            up = Vector3D.CrossProduct(right, forward);
            up.Normalize();

            double radFov = Math.Max(1, Math.Min(179, fov)) * Math.PI / 180.0;
            double frustumLen = modelScale * 0.5;
            double tanHalf = Math.Tan(radFov / 2.0);
            double hHalf = frustumLen * tanHalf;
            double wHalf = hHalf * aspectRatio;

            Point3D cEnd = camPos + forward * frustumLen;
            Point3D tr = cEnd + (up * hHalf) + (right * wHalf);
            Point3D tl = cEnd + (up * hHalf) - (right * wHalf);
            Point3D br = cEnd - (up * hHalf) + (right * wHalf);
            Point3D bl = cEnd - (up * hHalf) - (right * wHalf);

            AddFrustumMesh(camVisual, camPos, tr, tl, bl, br);
            AddSphereToMesh(targetVisual, targetPos, modelScale * 0.05, sphereDiv, sphereDiv);
        }

        public static Model3DGroup CreateViewCube(out GeometryModel3D[] faces, out GeometryModel3D[] corners)
        {
            var group = new Model3DGroup();
            var gray = new DiffuseMaterial(Brushes.Gray);
            var centerMesh = new MeshGeometry3D();
            AddCubeToMesh(centerMesh, new Point3D(0, 0, 0), 0.7);
            group.Children.Add(new GeometryModel3D(centerMesh, gray));

            faces = new GeometryModel3D[6];
            faces[0] = CreateFace(new Vector3D(1, 0, 0), new DiffuseMaterial(Brushes.Red), Texts.ViewRight);
            faces[1] = CreateFace(new Vector3D(-1, 0, 0), new DiffuseMaterial(Brushes.DarkRed), Texts.ViewLeft);
            faces[2] = CreateFace(new Vector3D(0, 1, 0), new DiffuseMaterial(Brushes.Lime), Texts.ViewTop);
            faces[3] = CreateFace(new Vector3D(0, -1, 0), new DiffuseMaterial(Brushes.Green), Texts.ViewBottom);
            faces[4] = CreateFace(new Vector3D(0, 0, 1), new DiffuseMaterial(Brushes.Blue), Texts.ViewFront);
            faces[5] = CreateFace(new Vector3D(0, 0, -1), new DiffuseMaterial(Brushes.DarkBlue), Texts.ViewBack);
            foreach (var f in faces) group.Children.Add(f);

            corners = new GeometryModel3D[8];
            corners[0] = CreateCorner(new Vector3D(1, 1, 1), Texts.CornerFRT);
            corners[1] = CreateCorner(new Vector3D(-1, 1, 1), Texts.CornerFLT);
            corners[2] = CreateCorner(new Vector3D(1, 1, -1), Texts.CornerBRT);
            corners[3] = CreateCorner(new Vector3D(-1, 1, -1), Texts.CornerBLT);
            corners[4] = CreateCorner(new Vector3D(1, -1, 1), Texts.CornerFRB);
            corners[5] = CreateCorner(new Vector3D(-1, -1, 1), Texts.CornerFLB);
            corners[6] = CreateCorner(new Vector3D(1, -1, -1), Texts.CornerBRB);
            corners[7] = CreateCorner(new Vector3D(-1, -1, -1), Texts.CornerBLB);
            foreach (var c in corners) group.Children.Add(c);

            return group;
        }

        private static GeometryModel3D CreateFace(Vector3D dir, Material mat, string name)
        {
            var mesh = new MeshGeometry3D();
            var center = dir * 0.85;
            AddCubeToMesh(mesh, new Point3D(center.X, center.Y, center.Z), 0.6);
            var model = new GeometryModel3D(mesh, mat);
            model.SetValue(FrameworkElement.TagProperty, name);
            return model;
        }

        private static GeometryModel3D CreateCorner(Vector3D dir, string name)
        {
            var mesh = new MeshGeometry3D();
            dir.Normalize();
            var center = dir * 0.85;
            AddCubeToMesh(mesh, new Point3D(center.X, center.Y, center.Z), 0.25);
            var mat = new DiffuseMaterial(Brushes.LightGray);
            var model = new GeometryModel3D(mesh, mat);
            model.SetValue(FrameworkElement.TagProperty, name);
            return model;
        }

        private static void AddArrow(MeshGeometry3D mesh, Point3D start, Vector3D dir, double len, double thick, int segs)
        {
            AddLineToMesh(mesh, start, start + dir * len, thick);
            AddConeToMesh(mesh, start + dir * len, dir, thick * 2.5, thick * 5, segs);
        }

        private static void AddConeToMesh(MeshGeometry3D mesh, Point3D tip, Vector3D dir, double radius, double height, int segs)
        {
            dir.Normalize();
            var perp1 = Vector3D.CrossProduct(dir, new Vector3D(0, 1, 0));
            if (perp1.LengthSquared < 0.001) perp1 = Vector3D.CrossProduct(dir, new Vector3D(1, 0, 0));
            perp1.Normalize();
            var perp2 = Vector3D.CrossProduct(dir, perp1);
            Point3D centerBase = tip - dir * height;
            int tipIdx = mesh.Positions.Count;
            mesh.Positions.Add(tip);
            int baseCenterIdx = mesh.Positions.Count;
            mesh.Positions.Add(centerBase);
            int baseStartIdx = mesh.Positions.Count;

            for (int i = 0; i < segs; i++)
            {
                double angle = i * 2 * Math.PI / segs;
                var pt = centerBase + perp1 * radius * Math.Cos(angle) + perp2 * radius * Math.Sin(angle);
                mesh.Positions.Add(pt);
            }

            for (int i = 0; i < segs; i++)
            {
                int next = (i + 1) % segs;
                mesh.TriangleIndices.Add(tipIdx);
                mesh.TriangleIndices.Add(baseStartIdx + i);
                mesh.TriangleIndices.Add(baseStartIdx + next);
                mesh.TriangleIndices.Add(baseCenterIdx);
                mesh.TriangleIndices.Add(baseStartIdx + next);
                mesh.TriangleIndices.Add(baseStartIdx + i);
            }
        }

        private static void AddQuad(MeshGeometry3D mesh, Point3D center, Vector3D normal, double size)
        {
            normal.Normalize();
            var u = Vector3D.CrossProduct(normal, new Vector3D(0, 1, 0));
            if (u.LengthSquared < 0.001) u = Vector3D.CrossProduct(normal, new Vector3D(1, 0, 0));
            u.Normalize();
            var v = Vector3D.CrossProduct(normal, u);
            double s = size / 2;
            Point3D p0 = center - u * s - v * s;
            Point3D p1 = center + u * s - v * s;
            Point3D p2 = center + u * s + v * s;
            Point3D p3 = center - u * s + v * s;
            int idx = mesh.Positions.Count;
            mesh.Positions.Add(p0); mesh.Positions.Add(p1); mesh.Positions.Add(p2); mesh.Positions.Add(p3);
            mesh.TriangleIndices.Add(idx); mesh.TriangleIndices.Add(idx + 1); mesh.TriangleIndices.Add(idx + 2);
            mesh.TriangleIndices.Add(idx); mesh.TriangleIndices.Add(idx + 2); mesh.TriangleIndices.Add(idx + 3);
            mesh.TriangleIndices.Add(idx); mesh.TriangleIndices.Add(idx + 2); mesh.TriangleIndices.Add(idx + 1);
            mesh.TriangleIndices.Add(idx); mesh.TriangleIndices.Add(idx + 3); mesh.TriangleIndices.Add(idx + 2);
        }

        private static void AddFrustumMesh(MeshGeometry3D mesh, Point3D o, Point3D tr, Point3D tl, Point3D bl, Point3D br)
        {
            AddLineToMesh(mesh, o, tr, 0.01); AddLineToMesh(mesh, o, tl, 0.01);
            AddLineToMesh(mesh, o, br, 0.01); AddLineToMesh(mesh, o, bl, 0.01);
            AddLineToMesh(mesh, tr, tl, 0.01); AddLineToMesh(mesh, tl, bl, 0.01);
            AddLineToMesh(mesh, bl, br, 0.01); AddLineToMesh(mesh, br, tr, 0.01);

            int idx = mesh.Positions.Count;
            mesh.Positions.Add(o); mesh.Positions.Add(tr); mesh.Positions.Add(tl); mesh.Positions.Add(bl); mesh.Positions.Add(br);
            int[] indices = { 0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 1, 4, 3, 2, 4, 2, 1 };
            foreach (var i in indices) mesh.TriangleIndices.Add(idx + i);
        }

        private static void AddCubeToMesh(MeshGeometry3D mesh, Point3D center, double size)
        {
            double s = size / 2.0;
            Point3D[] p = { new(center.X - s, center.Y - s, center.Z + s), new(center.X + s, center.Y - s, center.Z + s), new(center.X + s, center.Y + s, center.Z + s), new(center.X - s, center.Y + s, center.Z + s), new(center.X - s, center.Y - s, center.Z - s), new(center.X + s, center.Y - s, center.Z - s), new(center.X + s, center.Y + s, center.Z - s), new(center.X - s, center.Y + s, center.Z - s) };
            int idx = mesh.Positions.Count;
            foreach (var pt in p) mesh.Positions.Add(pt);
            int[] indices = { 0, 1, 2, 0, 2, 3, 4, 6, 5, 4, 7, 6, 0, 4, 5, 0, 5, 1, 1, 5, 6, 1, 6, 2, 2, 6, 7, 2, 7, 3, 3, 7, 4, 3, 4, 0 };
            foreach (var i in indices) mesh.TriangleIndices.Add(idx + i);
        }

        private static void AddLineToMesh(MeshGeometry3D mesh, Point3D start, Point3D end, double thickness)
        {
            var vec = end - start;
            if (vec.LengthSquared < double.Epsilon) return;
            vec.Normalize();
            var p1 = Vector3D.CrossProduct(vec, new Vector3D(0, 1, 0));
            if (p1.LengthSquared < 0.001) p1 = Vector3D.CrossProduct(vec, new Vector3D(1, 0, 0));
            p1.Normalize();
            var p2 = Vector3D.CrossProduct(vec, p1);
            p1 *= thickness; p2 *= thickness;
            int idx = mesh.Positions.Count;
            mesh.Positions.Add(start - p1 - p2); mesh.Positions.Add(start + p1 - p2);
            mesh.Positions.Add(start + p1 + p2); mesh.Positions.Add(start - p1 + p2);
            mesh.Positions.Add(end - p1 - p2); mesh.Positions.Add(end + p1 - p2);
            mesh.Positions.Add(end + p1 + p2); mesh.Positions.Add(end - p1 + p2);
            int[] indices = { 0, 1, 2, 0, 2, 3, 4, 6, 5, 4, 7, 6, 0, 4, 5, 0, 5, 1, 1, 5, 6, 1, 6, 2, 2, 6, 7, 2, 7, 3, 3, 7, 4, 3, 4, 0 };
            foreach (var i in indices) mesh.TriangleIndices.Add(idx + i);
        }

        private static void AddSphereToMesh(MeshGeometry3D mesh, Point3D center, double radius, int tDiv, int pDiv)
        {
            int baseIdx = mesh.Positions.Count;
            for (int pi = 0; pi <= pDiv; pi++)
            {
                double phi = pi * Math.PI / pDiv;
                for (int ti = 0; ti <= tDiv; ti++)
                {
                    double theta = ti * 2 * Math.PI / tDiv;
                    var pt = new Point3D(center.X + radius * Math.Sin(phi) * Math.Cos(theta), center.Y + radius * Math.Cos(phi), center.Z + radius * Math.Sin(phi) * Math.Sin(theta));
                    mesh.Positions.Add(pt);
                }
            }
            for (int pi = 0; pi < pDiv; pi++)
            {
                for (int ti = 0; ti < tDiv; ti++)
                {
                    int x0 = ti; int x1 = ti + 1; int y0 = pi * (tDiv + 1); int y1 = (pi + 1) * (tDiv + 1);
                    mesh.TriangleIndices.Add(baseIdx + y0 + x0); mesh.TriangleIndices.Add(baseIdx + y1 + x0); mesh.TriangleIndices.Add(baseIdx + y0 + x1);
                    mesh.TriangleIndices.Add(baseIdx + y1 + x0); mesh.TriangleIndices.Add(baseIdx + y1 + x1); mesh.TriangleIndices.Add(baseIdx + y0 + x1);
                }
            }
        }
    }
}