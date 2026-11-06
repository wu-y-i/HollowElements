using System.Linq;
using UnityEngine;

namespace HollowElements.Game.Bosses
{
    // Detached native emitters and pool actors may not be children of their boss.
    internal static class BossActors
    {
        private static readonly System.Collections.Generic.Dictionary<System.Type,BossBehaviour> owners=new System.Collections.Generic.Dictionary<System.Type,BossBehaviour>();
        private static T Owner<T>() where T:BossBehaviour
        {
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();BossBehaviour cached;
            if(owners.TryGetValue(typeof(T),out cached) && cached && cached.gameObject.scene==scene) return (T)cached;
            var found=Object.FindObjectsOfType<T>().FirstOrDefault(b=>b.gameObject.scene==scene);
            if(found) owners[typeof(T)]=found;return found;
        }
        internal static void Observe(GameObject obj)
        {
            if(!obj) return;
            string scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,name=obj.name;
            if((scene=="GG_Nosk" || scene=="GG_Nosk_V" || scene=="GG_Nosk_Hornet") && name.StartsWith("Vomit Glob Nosk")) Owner<NoskBoss>()?.Glob(obj);
            if(scene=="GG_Hornet_2" && name.StartsWith("Hornet Barb")) Owner<HornetSentinelBoss>()?.Barb(obj);
            if((scene=="GG_Ghost_No_Eyes" || scene=="GG_Ghost_No_Eyes_V") && name.StartsWith("No Eyes Head")) Owner<NoEyesBoss>()?.Ghost(obj);
            if(scene=="GG_Sly" && (name=="Spin Tink" || name=="Cyclone Tink" || name=="Shockwave")) Owner<SlyBoss>()?.Link(obj);
            if(scene=="GG_Ghost_Markoth" || scene=="GG_Ghost_Markoth_V")
            {
                if(name.StartsWith("Shot Markoth Nail")) Owner<MarkothBoss>()?.Nail(obj);
                if(name.StartsWith("Markoth Shield")) Owner<MarkothBoss>()?.ShieldRoot(obj);
            }
            if(scene=="GG_Grey_Prince_Zote" && name.StartsWith("Gas Explosion M")) Owner<GreyPrinceBoss>()?.Explosion(obj);
            if(scene=="GG_Hollow_Knight")
            {
                if(name.StartsWith("Shot HK") || name.StartsWith("HK Plume Prime")) Owner<PureVesselBoss>()?.Projectile(obj,null);
                if(name.StartsWith("HK Prime Blast") || name=="Focus Hit") Owner<PureVesselBoss>()?.Blast(obj);
            }
            if(scene=="GG_Radiance" && (name.StartsWith("Radiant ") || name=="Ascend Beam")) Owner<RadianceBoss>()?.Attack(obj);
        }
        internal static int BindingCount => Resources.FindObjectsOfTypeAll<MarkothOrbit>().Length+Resources.FindObjectsOfTypeAll<MarkothShield>().Length+
            Resources.FindObjectsOfTypeAll<SlyAttack>().Length+Resources.FindObjectsOfTypeAll<RadianceAttack>().Length+Resources.FindObjectsOfTypeAll<ZoteWave>().Length+Resources.FindObjectsOfTypeAll<BarbElementRing>().Length;
        internal static void RemoveAll()
        {
            owners.Clear();
            foreach(var item in Resources.FindObjectsOfTypeAll<MarkothOrbit>()) Object.Destroy(item);
            foreach(var item in Resources.FindObjectsOfTypeAll<MarkothShield>()) Object.Destroy(item);
            foreach(var item in Resources.FindObjectsOfTypeAll<SlyAttack>()) Object.Destroy(item);
            foreach(var item in Resources.FindObjectsOfTypeAll<RadianceAttack>()) Object.Destroy(item);
            foreach(var item in Resources.FindObjectsOfTypeAll<ZoteWave>()) Object.Destroy(item);
        }
    }
}
