﻿using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Corruption
{
    public class Projectile_Smoking : Projectile_Laser
    {        
        private int ticksToDetonation;
        
        private bool istraveling = true;

        public Mesh drawingMesh;


        public override void PerformPreFiringTreatment()
        {
            Graphics.DrawMesh(MeshPool.plane10, this.DrawPos + new Vector3(0,0.1f,0), this.ExactRotation, this.def.DrawMatSingle, 0);
            base.Comps_PostDraw();
            //DetermineImpactExactPosition();
            //drawingScale = new Vector3(1f, 1f, (this.destination - this.ExactPosition).magnitude);
            drawingScale = new Vector3(1f, 1f, (this.ExactPosition - this.origin).magnitude);
            drawingPosition = this.ExactPosition + ((this.ExactPosition - this.origin) / 2) + Vector3.up * this.def.Altitude;
            drawingMatrix.SetTRS(drawingPosition, this.ExactRotation, drawingScale);
        }

        public override void Draw()
        {
            base.Comps_PostDraw();
            Graphics.DrawMesh(MeshPool.plane10, this.ExactPosition, this.ExactRotation, this.def.DrawMatSingle, 0);
            if (this.SmokeMaterial != null)
            {
                UnityEngine.Graphics.DrawMesh(MeshPool.plane10, drawingMatrix, SolidColorMaterials.NewSolidColorMaterial(Color.red, ShaderDatabase.Cutout), 0); //FadedMaterialPool.FadedVersionOf(SmokeMaterial, drawingIntensity), 0);
               // Graphics.DrawMesh(MeshPool.plane10, this.ExactPosition, new Quaternion(, this.SmokeMaterial, 0); 
            }
        }

        public override void GetPreFiringDrawingParameters()
        {
            base.GetPreFiringDrawingParameters();
            drawingScale = new Vector3(1f, 1f, (this.ExactPosition - this.origin).magnitude);
            drawingPosition = this.ExactPosition + ((this.ExactPosition - this.origin) / 2) + Vector3.up * this.def.Altitude;
            drawingMatrix.SetTRS(drawingPosition, this.ExactRotation, drawingScale);
        }

        public override void GetPostFiringDrawingParameters()
        {
            base.GetPostFiringDrawingParameters();
            drawingScale = new Vector3(1f, 1f, (this.destination - this.ExactPosition).magnitude);
            drawingPosition = this.ExactPosition + ((this.ExactPosition - this.origin) / 2) + Vector3.up * this.def.Altitude;
            drawingMatrix.SetTRS(drawingPosition, this.ExactRotation, drawingScale);
        }

        public override void Tick()
        {
            //  Log.Message("Tickng Ma Lazor");
            // Directly call the Projectile base Tick function (we want to completely override the Projectile Tick() function).
            //((ThingWithComponents)this).Tick(); // Does not work...
            try
            {
                this.DoBaseTick();
                if (tickCounter == 0)
                {
                    GetParametersFromXml();
                    PerformPreFiringTreatment();
                }

                // Pre firing.
                if (tickCounter < preFiringDuration)
                {
                    GetPreFiringDrawingParameters();
                    tickCounter++;
                }
                // Firing.
                else if (tickCounter > this.preFiringDuration && this.istraveling)
                {
                    Log.Message("Traveling");
                    GetPostFiringDrawingParameters();
                }
                // Post firing.
                else
                {
                    GetPostFiringDrawingParameters();
                    this.ticksToDetonation--;                
                    tickCounter++;
                }
                if ((tickCounter > (this.preFiringDuration + this.postFiringDuration)) && (!this.additionalParameters.causesExplosion || (this.additionalParameters.causesExplosion && this.ticksToDetonation > this.def.projectile.explosionDelay)) && !this.istraveling && !this.Destroyed)
                {
                    this.Destroy(DestroyMode.Vanish);
                }
                if (this.launcher != null)
                {
                    if (this.launcher is Pawn)
                    {
                        Pawn launcherPawn = this.launcher as Pawn;
                        if ((((launcherPawn.Dead) == true) && !this.Destroyed))
                        {
                            this.Destroy(DestroyMode.Vanish);
                        }
                    }
                }
            }
            catch
            {
                this.Destroy(DestroyMode.Vanish);
            }

        }

        protected override void Impact(Thing hitThing)
        {
            Map map = base.Map;
            if (this.additionalParameters.causesExplosion)
            {
                if (this.def.projectile.explosionDelay == 0)
                {
                    this.Explode();
                    return;
                }
                this.landed = true;
                this.ticksToDetonation = this.def.projectile.explosionDelay;
                GenExplosion.NotifyNearbyPawnsOfDangerousExplosive(this, this.def.projectile.damageDef, this.launcher.Faction);
            }
            else
            {
                if (hitThing != null)
                {
                    int damageAmountBase = this.def.projectile.damageAmountBase;
                    ThingDef equipmentDef = this.equipmentDef;
                    DamageInfo dinfo = new DamageInfo(this.def.projectile.damageDef, damageAmountBase, this.ExactRotation.eulerAngles.y, this.launcher, null, equipmentDef, DamageInfo.SourceCategory.ThingOrUnknown);
                    hitThing.TakeDamage(dinfo);
                }
                else
                {
                    SoundDefOf.BulletImpactGround.PlayOneShot(new TargetInfo(base.Position, map, false));
                    MoteMaker.MakeStaticMote(this.ExactPosition, map, ThingDefOf.Mote_ShotHit_Dirt, 1f);
                    if (base.Position.GetTerrain(map).takeSplashes)
                    {
                        MoteMaker.MakeWaterSplash(this.ExactPosition, map, Mathf.Sqrt((float)this.def.projectile.damageAmountBase) * 1f, 4f);
                    }
                }
            }
            this.istraveling = false;
        }

        protected virtual void Explode()
        {
            Map map = base.Map;
            this.Destroy(DestroyMode.Vanish);
            ThingDef preExplosionSpawnThingDef = this.def.projectile.preExplosionSpawnThingDef;
            float explosionSpawnChance = this.def.projectile.explosionSpawnChance;
            GenExplosion.DoExplosion(base.Position, map, this.def.projectile.explosionRadius, this.def.projectile.damageDef, this.launcher, this.def.projectile.soundExplode, this.def, this.equipmentDef, this.def.projectile.postExplosionSpawnThingDef, this.def.projectile.explosionSpawnChance, 1, false, preExplosionSpawnThingDef, explosionSpawnChance, 1);
        }

    }
}