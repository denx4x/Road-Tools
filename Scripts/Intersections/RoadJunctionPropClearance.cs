using UnityEngine;

namespace Dyma.SplineLevelToolkit
{
    internal static class RoadJunctionPropClearance
    {
        internal static float Padding(SplinePropLayer layer)
        {
            float extent=.15f;
            if(layer.Prefab!=null)
            {
                RoadFenceModel model=layer.ContinuousFence?layer.Prefab.GetComponent<RoadFenceModel>():null;
                if(model!=null && model.TryGetRootBounds(out Bounds bounds))
                    extent=Mathf.Max(Mathf.Abs(model.Across(bounds.min)),Mathf.Abs(model.Across(bounds.max)));
                else
                    foreach(MeshFilter filter in layer.Prefab.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if(filter.sharedMesh==null)continue;
                        Matrix4x4 matrix=Matrix4x4.Scale(layer.Prefab.transform.localScale)*
                            layer.Prefab.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                        Bounds mesh=filter.sharedMesh.bounds;
                        for(int i=0;i<8;i++)
                        {
                            Vector3 point=matrix.MultiplyPoint3x4(mesh.center+Vector3.Scale(mesh.extents,
                                new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                            extent=Mathf.Max(extent,layer.ContinuousFence?Mathf.Abs(point.x):new Vector2(point.x,point.z).magnitude);
                        }
                    }
            }
            if(!layer.ContinuousFence)extent*=Mathf.Max(1,layer.RandomScale.x,layer.RandomScale.y);
            return Mathf.Max(.4f,Mathf.Abs(layer.LateralOffset)+extent+.35f);
        }
    }
}
