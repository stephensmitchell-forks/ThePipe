﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PipeDataModel.Types.Geometry.Curve;
using PipeDataModel.Utils;

namespace PipeDataModel.Types.Geometry.Surface
{
    [Serializable]
    public class NurbsSurface : Surface, IEquatable<NurbsSurface>
    {
        #region fields
        //the points are stored in a dictionary to make the u,v indexing easy. The key is a hashed integer of u and v
        private Dictionary<int, Vec> _points = new Dictionary<int, Vec>();
        private Dictionary<int, double> _weights = new Dictionary<int, double>();
        private List<double> _uKnots, _vKnots = new List<double>();
        private int _uDegree, _vDegree;
        private int _uCount, _vCount;
        private bool _isClosedInU, _isClosedInV;
        private List<Curve.Curve> _trimCurves = new List<Curve.Curve>();
        private List<Curve.Curve> _innerTrims = new List<Curve.Curve>();
        #endregion

        #region properties
        public Dictionary<int, Vec> Points { get => _points; set => _points = value; }
        public int UDegree { get => _uDegree; set => _uDegree = value; }
        public int VDegree { get => _vDegree; set => _vDegree = value; }
        public int UCount { get => _uCount; set => _uCount = value; }
        public int VCount { get => _vCount; set => _vCount = value; }
        public List<double> UKnots { get => _uKnots; set => _uKnots = Utils.GeometryUtil.NormalizedKnots(value); }
        public List<double> VKnots { get => _vKnots; set => _vKnots = Utils.GeometryUtil.NormalizedKnots(value); }
        public bool IsClosedInU { get => _isClosedInU; set => _isClosedInU = value; }
        public bool IsClosedInV { get => _isClosedInV; set => _isClosedInV = value; }
        public List<Curve.Curve> OuterTrims { get => _trimCurves; }
        public List<Curve.Curve> InnerTrims { get => _innerTrims; }
        public List<Curve.Curve> TrimCurves
        {
            get
            {
                List<Curve.Curve> trims = new List<Curve.Curve>();
                trims.AddRange(_trimCurves);
                trims.AddRange(_innerTrims);
                return trims;
            }
        }
        #endregion

        #region constructors
        public NurbsSurface(int uCount, int vCount, int uDeg, int vDeg)
        {
            _uDegree = uDeg;
            _vDegree = vDeg;
            _uCount = uCount;
            _vCount = vCount;
        }
        public NurbsSurface(IEnumerable<Vec> points, int uCount, int vCount, int uDeg, int vDeg):this(uCount, vCount, uDeg, vDeg)
        {
            if(points.Count() != uCount * vCount) { throw new InvalidOperationException("Invalid data for creating a NurbsSurface"); }

            _points.Clear();
            for(int u = 0; u < _uCount; u++)
            {
                for (int v = 0; v < _vCount; v++)
                {
                    int i = u * _vCount + v;
                    _points[HashIndices(u, v)] = _points[i];
                }
            }
        }
        #endregion

        #region methods
        public override bool Equals(IPipeMemberType other)
        {
            if (!GetType().IsAssignableFrom(other.GetType()) || other == null) { return false; }
            return Equals((NurbsSurface)other);
        }
        public bool Equals(NurbsSurface surf)
        {
            return surf.UDegree == _uDegree && surf.VDegree == _vDegree && surf.UCount == _uCount && surf.VCount == _vCount
                && Utils.PipeDataUtil.EqualCollections(_points, surf.Points)
                && Utils.PipeDataUtil.EqualIgnoreOrder(_trimCurves, surf.OuterTrims);
        }

        public Vec GetControlPointAt(int u, int v)
        {
            Vec pt;
            if(!ValidUVIndices(u,v)) { throw new InvalidOperationException("Invalid u, v indices"); }
            if(_points.TryGetValue(HashIndices(u,v), out pt)) { return pt; }
            return null;
        }

        public List<Vec> GetControlPointsAsList()
        {
            List<Vec> pts = new List<Vec>();
            for (int u = 0; u < _uCount; u++)
            {
                for (int v = 0; v < _vCount; v++)
                {
                    pts.Add(GetControlPointAt(u, v));
                }
            }
            return pts;
        }

        public void SetControlPoint(Vec pt, int u, int v)
        {
            if (!ValidUVIndices(u, v)) { throw new InvalidOperationException("Invalid u, v indices"); }
            int hash = HashIndices(u, v);
            if (_points.ContainsKey(hash)) { _points[hash] = pt; }
            else { _points.Add(hash, pt); }
        }

        public double GetWeightAt(int u, int v)
        {
            double pt;
            if (!ValidUVIndices(u, v)) { throw new InvalidOperationException("Invalid u, v indices"); }
            if (_weights.TryGetValue(HashIndices(u, v), out pt)) { return pt; }
            return 1;
        }

        public void SetWeight(double weight, int u, int v)
        {
            if (!ValidUVIndices(u, v)) { throw new InvalidOperationException("Invalid u, v indices"); }
            int hash = HashIndices(u, v);
            if (_weights.ContainsKey(hash)) { _weights[hash] = weight; }
            else { _weights.Add(hash, weight); }
        }

        private bool ValidUVIndices(int u, int v)
        {
            return 0 <= u && u < _uCount && 0 <= v && v < _vCount;
        }

        public bool WrapPointsToCloseSurface(int direction)
        {
            if(direction == 0)
            {
                int u = _uCount++;
                for(int v = 0; v < _vCount; v++)
                {
                    Vec pt = GetControlPointAt(0, v);
                    SetControlPoint(pt, u, v);
                }
            }
            else if(direction == 1)
            {
                int v = _vCount++;
                for (int u = 0; u < _uCount; u++)
                {
                    Vec pt = GetControlPointAt(u, 0);
                    SetControlPoint(pt, u, v);
                }
            }
            else { return false; }

            return true;
        }
        private static int HashIndices(int u, int v)
        {
            //seed
            int hash = 19;
            hash = hash * 17 + u;
            hash = hash * 17 + v;
            return hash;
        }

        public override List<Vec> Vertices()
        {
            if (_trimCurves.Count == 0)
            {
                return new List<Vec>() {
                    GetControlPointAt(0, 0),
                    GetControlPointAt(_uCount-1, 0),
                    GetControlPointAt(_uCount-1, _vCount-1),
                    GetControlPointAt(0, _vCount-1),
                };
            }
            var verts = new List<Vec>();
            foreach(var curve in _trimCurves)
            {
                var curPts = curve.Vertices();
                foreach(var curPt in curPts)
                {
                    if (!GeometryUtil.ListContainsCoincidentPoint(verts, curPt))
                    {
                        verts.Add(curPt);
                    }
                }
            }

            return verts;
        }

        public override List<Curve.Curve> Edges()
        {
            if (_trimCurves.Count == 0)
            {
                Curve.Curve a, b, c, d;
                List<Vec> apts = new List<Vec>(), cpts = new List<Vec>();
                for(int u = 0; u < _uCount; u++)
                {
                    apts.Add(GetControlPointAt(u, 0));
                    cpts.Add(GetControlPointAt(u, _vCount - 1));
                }

                List<Vec> bpts = new List<Vec>(), dpts = new List<Vec>();
                for (int v = 0; v < _vCount; v++)
                {
                    bpts.Add(GetControlPointAt(0, v));
                    dpts.Add(GetControlPointAt(_uCount - 1, v));
                }

                a = new NurbsCurve(apts, _uDegree, _isClosedInU);
                b = new NurbsCurve(bpts, _vDegree, _isClosedInV);
                c = new NurbsCurve(cpts, _uDegree, _isClosedInU);
                d = new NurbsCurve(dpts, _vDegree, _isClosedInV);

                return new List<Curve.Curve>() { a, b, c, d };
            }

            return _trimCurves;
        }

        #endregion
    }
}
