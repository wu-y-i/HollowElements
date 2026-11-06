using HollowElements.Rules;
using HutongGames.PlayMaker;
using UnityEngine;

namespace HollowElements.Game.Bosses
{
    internal sealed class DungBallSplit : MonoBehaviour
    {
        internal DungDefenderBoss Owner;
        internal bool Small,Triggered;
        internal int Count;
        private GameObject prefab;
        private bool queued;
        internal void Configure(DungDefenderBoss owner,GameObject source,int stacks,bool small)
        {Owner=owner;prefab=source;Count=stacks>0?stacks+1:0;Small=small;Triggered=false;queued=false;}
        private void OnEnable() {Owner=null;prefab=null;Small=false;Triggered=false;queued=false;Count=0;}
        internal void Terrain(FsmState state)
        {
            if(state.Fsm.Name.StartsWith("Ball Control") && (state.Name=="Bounce" || state.Name=="Flip")) Request();
        }
        private void OnCollisionEnter2D(Collision2D collision)
        {if(collision.gameObject.layer==8) Request();}
        private void Request()
        {if(!Small && !Triggered && Count>0 && Owner && prefab) {Triggered=true;queued=true;}}
        private void LateUpdate()
        {
            if(!queued) return;queued=false;if(!Owner || !prefab) return;
            var marker=GetComponent<ElementalAttack>();Element element=marker?marker.Element:Element.None;
            var body=GetComponent<Rigidbody2D>();Vector2 velocity=body?body.velocity:Vector2.up*20;
            float angle=Mathf.Atan2(velocity.y,velocity.x)*Mathf.Rad2Deg;
            float speed=Mathf.Clamp(velocity.magnitude,15,35);
            for(int i=0;i<Count;i++)
            {
                float a=(angle+Mathf.Lerp(-40,40,(float)i/(Count-1)))*Mathf.Deg2Rad;
                Vector2 direction=new Vector2(Mathf.Cos(a),Mathf.Sin(a));
                var child=prefab.Spawn(transform.position+(Vector3)(direction*.45f),Quaternion.identity);
                (child.GetComponent<DungBallSplit>() ?? child.AddComponent<DungBallSplit>()).Configure(Owner,prefab,0,true);
                ElementalAttack.Set(child,element,Owner.GetComponent<HealthManager>().GetInstanceID(),size:.5f);
                var rb=child.GetComponent<Rigidbody2D>();if(rb) rb.velocity=direction*speed;
            }
            // Recycle after native collision dispatch, never from inside its FSM.
            gameObject.Recycle();
        }
    }
}
