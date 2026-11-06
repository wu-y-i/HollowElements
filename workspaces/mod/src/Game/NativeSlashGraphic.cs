using UnityEngine;

namespace HollowElements.Game
{
    // Draw a borrowed native tk2d frame with its actual geometry/UVs, including
    // atlas rotation and trimming. No texture extraction or shared material edits.
    internal sealed class NativeSlashGraphic : UnityEngine.UI.MaskableGraphic
    {
        internal tk2dSpriteDefinition Definition;
        internal string DefinitionName => Definition?.name;
        public override Texture mainTexture
        {
            get
            {
                var source=Definition?.materialInst ?? Definition?.material;
                return source && source.mainTexture ? source.mainTexture : Texture2D.whiteTexture;
            }
        }
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper helper)
        {
            helper.Clear();
            if(Definition==null || Definition.positions==null || Definition.positions.Length<3) return;
            var positions=Definition.positions;
            Vector3 min=positions[0],max=min;
            foreach(var p in positions) { min=Vector3.Min(min,p); max=Vector3.Max(max,p); }
            Vector3 center=(min+max)*.5f;
            var rect=rectTransform.rect;
            float scale=Mathf.Min(rect.width/Mathf.Max(.001f,max.x-min.x),rect.height/Mathf.Max(.001f,max.y-min.y));
            for(int i=0;i<positions.Length;i++)
                helper.AddVert((positions[i]-center)*scale+(Vector3)rect.center,color,Definition.uvs[i]);
            for(int i=0;i<Definition.indices.Length;i+=3)
                helper.AddTriangle(Definition.indices[i],Definition.indices[i+1],Definition.indices[i+2]);
        }
    }
}
