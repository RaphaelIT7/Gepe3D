
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;

namespace Gepe3D
{
    public class FluidConstraint
    {
        
        float restDensity;
        int[] particleIDs;
        // Particle[] fluidParticles;
        List<Particle>[] neighbours;
        Vector3[] corrections;
        Dictionary<Particle, float> lambdas;
        
        
        // kernel values
        float h, poly6coeff, spikyGradCoeff;
        
        // Epsilon in gamma correction denominator
        float RELAXATION = 0.01f;
        
        // Pressure terms
        float K_P  = 0.1f;
        float E_P  = 4.0f;
        float DQ_P = 0.2f;
        
        
        public FluidConstraint(int[] particleIDs, float restDensity, float particleEffectRadius)
        {
            this.particleIDs = particleIDs;
            this.restDensity = restDensity;
            this.lambdas = new Dictionary<Particle, float>();
            this.corrections = new Vector3[particleIDs.Length];
            this.neighbours = new List<Particle>[particleIDs.Length];
            for (int i = 0; i < particleIDs.Length; i++)
                neighbours[i] = new List<Particle>();
            
            // kernel values
            this.h  = particleEffectRadius;
            this.poly6coeff = 315f / ( 64f * MathF.PI * MathF.Pow(h, 9) );
            this.spikyGradCoeff = -45 / (MathF.PI * MathF.Pow(h, 6) );
        }
        
        
        private float Kernel_Poly6(float dist)
        {
            dist = MathHelper.Clamp(dist, 0, h);
            float t = h * h - dist * dist;
            return poly6coeff * t * t * t;
        }
        
        private float Kernel_SpikyGrad(float dist)
        {
            dist = MathHelper.Clamp(dist, 0, h);
            if (dist == 0) return 0; // spiky gradient can't be used when its the same particle
            return spikyGradCoeff * (h - dist) * (h - dist);
        }
        
        
        private float GetDist2(Vector3 v1, Vector3 v2)
        {
            return
                (v1.X - v2.X) * (v1.X - v2.X) +
                (v1.Y - v2.Y) * (v1.Y - v2.Y) +
                (v1.Z - v2.Z) * (v1.Z - v2.Z);
        }
        
        public void Project(Particle[] allParticles, List<int>[][][] grid)
        {
            
            int startX, startY, startZ, endX, endY, endZ;
            
            for (int i = 0; i < particleIDs.Length; i++)
            {
                Particle p1 = allParticles[ particleIDs[i] ];
                neighbours[i].Clear();
                float density = 0;
                float denominator = 0;
                Vector3 denom2 = new Vector3();
                
                
                startX = Math.Max(0, p1.gridX - 1);
                startY = Math.Max(0, p1.gridY - 1);
                startZ = Math.Max(0, p1.gridZ - 1);
                endX = Math.Min(ParticleSimulator.GridRowsX - 1, p1.gridX + 1);
                endY = Math.Min(ParticleSimulator.GridRowsY - 1, p1.gridY + 1);
                endZ = Math.Min(ParticleSimulator.GridRowsZ - 1, p1.gridZ + 1);
                
                for (int gx = startX; gx <= endX; gx++) {
                    for (int gy = startY; gy <= endY; gy++) {
                        for (int gz = startZ; gz <= endZ; gz++) {
                            
                            foreach (int j in grid[gx][gy][gz]) {
                                Particle p2 = allParticles[j];
                
                
                // foreach (Particle p2 in allParticles)
                // {
                    if (p2.inverseMass == 0) continue;

                    Vector3 diff = p1.posEstimate - p2.posEstimate;
                    float dist = diff.Length;
                    

                    if (dist < h)
                    {
                        
                        neighbours[i].Add(p2); // it will add itself as well

                        // the added bit should be multiplied by an extra scalar if its a solid
                        density += (1f / p2.inverseMass) * Kernel_Poly6(dist);
                        if (p1 != p2) {
                            
                            float kernelVal = Kernel_SpikyGrad(dist);
                            
                            // gradient of neighbours
                            float constraintGradient = kernelVal / restDensity;
                            denominator += constraintGradient * constraintGradient;
                            
                            // gradient of self
                            denom2 += kernelVal * diff.Normalized();
                            
                        }
                        
                    }
                }
                        }
                    }
                }
                denom2 /= restDensity;
                denominator += Vector3.Dot(denom2, denom2); // add dist squared
                
                lambdas[p1] = -( density / restDensity - 1 ) / (denominator + RELAXATION);
                
                if (i == 27) System.Console.WriteLine(density);
            }
            
            
            for (int i = 0; i < particleIDs.Length; i++)
            {
                Particle p1 = allParticles[ particleIDs[i] ];
                Vector3 correction = new Vector3();
                
                foreach (Particle p2 in neighbours[i])
                {
                    if (p1 == p2) continue;
                    
                    // Vector3 diff = p1.posEstimate - p2.posEstimate;
                    Vector3 diff = new Vector3(p1.posEstimate.X - p2.posEstimate.X, p1.posEstimate.Y - p2.posEstimate.Y, p1.posEstimate.Z - p2.posEstimate.Z);
                    
                    
                    Vector3 grad = Kernel_SpikyGrad( diff.Length ) * diff.Normalized();
                    
                    float lambdaCorr = -K_P * MathF.Pow( Kernel_Poly6(diff.Length) / Kernel_Poly6(DQ_P * h), E_P );
                    // lambdaCorr = 0;
                    
                    float neighbourLambda = lambdas.ContainsKey(p2) ? lambdas[p2] : 0;
                    
                    correction += (lambdas[p1] + neighbourLambda + lambdaCorr) * grad;
                    
                }
                
                corrections[i] = correction / restDensity;
            }
            
            // must wait till all posEstimate are done
            for (int i = 0; i < particleIDs.Length; i++)
            {
                Particle p1 = allParticles[ particleIDs[i] ];
                p1.posEstimate += corrections[i] / (float) (neighbours[i].Count + p1.constraintCount);
            }
            
        }
        
        
    }
}