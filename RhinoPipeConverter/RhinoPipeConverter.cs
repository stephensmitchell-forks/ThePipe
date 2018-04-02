﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using PipeDataModel.Types;
using PipeDataModel.Utils;
using rh = Rhino.Geometry;
using pp = PipeDataModel.Types.Geometry;
using pps = PipeDataModel.Types.Geometry.Surface;
using ppc = PipeDataModel.Types.Geometry.Curve;


namespace RhinoPipeConverter
{
    public class GeometryConverter:PipeConverter<rh.GeometryBase, IPipeMemberType>
    {
        private static Point3dConverter _pt3dConv = new Point3dConverter();
        private static Point3fConverter _pt3fConv = new Point3fConverter();
        private static Vector3DConverter _vec3DConv = new Vector3DConverter();
        private static PlaneConverter _planeConv = new PlaneConverter(_vec3DConv, _pt3dConv);
        private static ArcConverter _arcConv = new ArcConverter(_planeConv, _pt3dConv);
        private static LineConverter _lineConv = new LineConverter(_pt3dConv);

        public GeometryConverter()
        {
            var ptConv = AddConverter(new PointConverter(_pt3dConv));
            var curveConv = new CurveConverter(_pt3dConv, _arcConv, _lineConv);
            AddConverter(curveConv);
            var meshConv = AddConverter(new MeshConverter(_pt3fConv));

            var surfaceConverter = new SurfaceConverter(curveConv, _vec3DConv, _pt3dConv);
            AddConverter(surfaceConverter);

            var brepConv = new BrepConverter(surfaceConverter, curveConv, _pt3dConv);
            AddConverter(brepConv);
        }
    }

    public class MeshConverter: PipeConverter<rh.Mesh, pp.Mesh>
    {
        public MeshConverter(Point3fConverter ptConv):
            base(
                    (rhm) => {
                        return new pp.Mesh(rhm.Vertices.Select((v) => ptConv.ToPipe<rh.Point3f, pp.Vec>(v)).ToList(),
                            rhm.Faces.Select(
                                (f) => f.IsTriangle ? new ulong[] { (ulong)f.A, (ulong)f.B, (ulong)f.C } : 
                                    new ulong[] { (ulong)f.A, (ulong)f.B, (ulong)f.C, (ulong)f.D }).ToList()
                            );
                    },
                    (ppm) => {
                        var mesh = new rh.Mesh();
                        mesh.Vertices.AddVertices(ppm.Vertices.Select((v) => ptConv.FromPipe<rh.Point3f, pp.Vec>(v)));
                        mesh.Faces.AddFaces(ppm.Faces.Select(
                            (f) => pp.Mesh.FaceIsTriangle(f) ? new rh.MeshFace((int)f[0], (int)f[1], (int)f[2]) : 
                                new rh.MeshFace((int)f[0], (int)f[1], (int)f[2], (int)f[3])
                        ));
                        return mesh;
                    }
                )
        { }
    }

    public class BrepConverter: PipeConverter<rh.Brep, pps.PolySurface>
    {
        public BrepConverter(SurfaceConverter surfConv, CurveConverter curveConv, Point3dConverter ptConv) : 
            base(
                    (rb) => {
                        List<pps.Surface> faces = new List<pps.Surface>();
                        for(int i = 0; i < rb.Faces.Count; i++)
                        {
                            var surf = (pps.NurbsSurface)surfConv.ToPipe<rh.Surface, pps.Surface>(rb.Faces[i].ToNurbsSurface());
                            surf.TrimCurves.Clear();
                            surf.TrimCurves.AddRange(rb.Faces[i].Loops.Select((l) => curveConv.ToPipe<rh.Curve, ppc.Curve>(l.To3dCurve())));
                            faces.Add(surf);
                        }
                        var polySurf = new pps.PolySurface(faces);

                        return polySurf;
                    },
                    (pb) => {
                        if(pb.Surfaces.Count <= 0) { return null; }
                        rh.Brep brep;
                        if(pb.Surfaces.Count == 1)
                        {
                            var surf = pb.Surfaces.FirstOrDefault();
                            brep = rh.Brep.CreateFromSurface(surfConv.FromPipe<rh.Surface, pps.Surface>(surf));
                            if (typeof(pps.NurbsSurface).IsAssignableFrom(surf.GetType())
                                    && ((pps.NurbsSurface)surf).TrimCurves.Count > 0)
                            {
                                List<ppc.Curve> trims = ((pps.NurbsSurface)surf).TrimCurves;
                                List<ppc.Curve> loops = brep.Faces.First().Loops.Select((l) => 
                                    curveConv.ToPipe<rh.Curve, ppc.Curve>(l.To3dCurve())).ToList();

                                if(!PipeDataUtil.EqualIgnoreOrder(loops, trims))
                                {
                                    var brep2 = brep.Faces.First().Split(trims.Select((c) => 
                                        curveConv.FromPipe<rh.Curve, ppc.Curve>(c)).ToList(), Rhino.RhinoMath.ZeroTolerance);
                                    if (brep2 != null) { brep = brep2.Faces.Last().DuplicateFace(false); }
                                }
                            }
                        }
                        else
                        {
                            brep = rh.Brep.MergeBreps(pb.Surfaces.Select((s) => {
                                var subrep = rh.Brep.CreateFromSurface(surfConv.FromPipe<rh.Surface, pps.Surface>(s));
                                if(typeof(pps.NurbsSurface).IsAssignableFrom(s.GetType()) 
                                    && ((pps.NurbsSurface)s).TrimCurves.Count > 0)
                                {
                                    List<ppc.Curve> trims = ((pps.NurbsSurface)s).TrimCurves;
                                    List<ppc.Curve> loops = subrep.Faces.First().Loops.Select((l) =>
                                        curveConv.ToPipe<rh.Curve, ppc.Curve>(l.To3dCurve())).ToList();

                                    if (!PipeDataUtil.EqualIgnoreOrder(loops, trims))
                                    {
                                        var brep2 = subrep.Faces.First().Split(trims.Select((c) =>
                                            curveConv.FromPipe<rh.Curve, ppc.Curve>(c)).ToList(), Rhino.RhinoMath.ZeroTolerance);
                                        if (brep2 != null) { brep = brep2.Faces.Last().DuplicateFace(false); }
                                    }
                                }
                                return subrep;
                            }), Rhino.RhinoMath.ZeroTolerance);
                        }
                        return brep;
                    }
                )
        { }
    }
}
