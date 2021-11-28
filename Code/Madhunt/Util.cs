using System;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Celeste;
using Monocle;

namespace Celeste.Mod.Madhunt {
    public static class RandomHelper {
        public static Vector2 NextVector(this Random rng, float magnitude) {
            return Vector2.UnitX.Rotate(rng.NextAngle()) * magnitude;
        }
    }

    public static class ColorHelper {
        private const int FUZZINESS = 8;
        private static readonly float SQRT_ONE_THIRD = (float) Math.Sqrt(1f/3f);

        public static Matrix CalculateHueShiftMatrix(float hueShift) {
            float sinShift = (float) Math.Sin(hueShift / 180 * Math.PI), cosShift = (float) Math.Cos(hueShift / 180 * Math.PI);
            return new Matrix(
                cosShift + (1-cosShift) / 3, 1f/3f * (1-cosShift) + SQRT_ONE_THIRD * sinShift, 1f/3f * (1-cosShift) - SQRT_ONE_THIRD * sinShift, 0,
                1f/3f * (1-cosShift) - SQRT_ONE_THIRD * sinShift, cosShift + (1-cosShift) / 3, 1f/3f * (1-cosShift) + SQRT_ONE_THIRD * sinShift, 0,
                1f/3f * (1-cosShift) + SQRT_ONE_THIRD * sinShift, 1f/3f * (1-cosShift) - SQRT_ONE_THIRD * sinShift, cosShift + (1-cosShift) / 3, 0,
                0, 0, 0, 1
            );
        }

        public static Color RemoveAlpha(this Color color) {
            return new Color(color.R, color.G, color.B, 255);
        }

        public static bool IsApproximately(this Color color, params Color[] others) {
            foreach(Color other in others) {
                if(
                    Math.Abs(color.R - other.R) <= FUZZINESS && 
                    Math.Abs(color.B - other.B) <= FUZZINESS && 
                    Math.Abs(color.G - other.G) <= FUZZINESS && 
                    Math.Abs(color.A - other.A) <= FUZZINESS
                ) return true;
            }
            return false;
        }

        public static float GetHue(this Color color) {
            float minComp = Calc.Min(color.R, color.G, color.B), maxComp = Calc.Max(color.R, color.G, color.B);
            if(maxComp == color.R) return 60 * (color.G - color.B) / (maxComp-minComp);
            else if(maxComp == color.G) return 60 * (2 + (color.B - color.R) / (maxComp-minComp));
            else return 60 * (4 + (color.R - color.G) / (maxComp-minComp));
        }

        public static Color ShiftColor(this Color color, Matrix hueShift, float intensityShift) {
            Vector3 shiftedRGB = Vector3.Transform(color.ToVector3(), hueShift);
            if(color.IsApproximately(Color.White)) return new Color(new Vector4(shiftedRGB, color.A));
            else return new Color(new Vector4(shiftedRGB * intensityShift, color.A));
        }

        public static ParticleType ShiftColor(this ParticleType type, Matrix hueShift, float intensityShift) => new ParticleType(type) {
            Color = type.Color.ShiftColor(hueShift, intensityShift),
            Color2 = type.Color2.ShiftColor(hueShift, intensityShift)
        };

        public static Color Recolor(this Color col, Color origCol, Color newCol) => col.ShiftColor(CalculateHueShiftMatrix(newCol.GetHue() - origCol.GetHue()), (float) (newCol.R+newCol.G+newCol.B) / (origCol.R+origCol.G+origCol.B));
        public static ParticleType Recolor(this ParticleType type, Color origCol, Color newCol) => type.ShiftColor(CalculateHueShiftMatrix(newCol.GetHue() - origCol.GetHue()), (float) (newCol.R+newCol.G+newCol.B) / (origCol.R+origCol.G+origCol.B));

    }

    public static class TextureHelper {
        private static readonly Dictionary<VirtualTexture, TextureData> TEX_DATA_CACHE = new Dictionary<VirtualTexture, TextureData>();
        
        public static TextureData GetTextureData(this VirtualTexture tex) {
            if(!TEX_DATA_CACHE.TryGetValue(tex, out TextureData data)) {
                TEX_DATA_CACHE[tex] = data = new TextureData(tex.Width, tex.Height);
                tex.Texture_Safe.GetData<Color>(data.Pixels);
            }
            return data;
        }

        public static TextureData GetTextureData(this MTexture tex) => GetTextureData(tex.Texture).GetSubsection(tex.ClipRect);
    }

    public static class SpriteHelper {
        private static readonly Func<Sprite, Sprite, Sprite> CLONEINTO_DELEG = (Func<Sprite, Sprite, Sprite>) typeof(Sprite).GetMethod("CloneInto", BindingFlags.NonPublic | BindingFlags.Instance).CreateDelegate(typeof(Func<Sprite, Sprite, Sprite>));
        public static Sprite CloneInto(this Sprite sprite, Sprite clone) => CLONEINTO_DELEG(sprite, clone);
        
        public static Sprite Recolor(this Sprite sprite, Color origCol, Color newCol) {
            //Calculate hue and intensity shift
            Matrix hueShift = ColorHelper.CalculateHueShiftMatrix(newCol.GetHue() - origCol.GetHue());
            float intensityShift = (float) (newCol.R+newCol.G+newCol.B) / (origCol.R+origCol.G+origCol.B);

            //Filter animation frames
            TextureHeap heap = new TextureHeap();
            Dictionary<Sprite.Animation, Rectangle[]> animFrames = new Dictionary<Sprite.Animation, Rectangle[]>();
            foreach(var anim in sprite.Animations) {
                Rectangle[] frames = animFrames[anim.Value] = new Rectangle[anim.Value.Frames.Length];
                for(int i = 0; i < frames.Length; i++) {
                    //Shift pixel hues
                    TextureData data = anim.Value.Frames[i].GetTextureData();
                    foreach(Point p in data) data[p] = data[p].ShiftColor(hueShift, intensityShift);
                    frames[i] = heap.AddTexture(data);
                }
            }

            TextureData heapTexData = heap.CreateHeapTexture();
            MTexture heapTex = new MTexture(VirtualContent.CreateTexture($"filteredSprite<{sprite.GetHashCode()}:{origCol}-{newCol}>", heapTexData.Width, heapTexData.Height, Color.White));
            heapTex.Texture.Texture_Safe.SetData<Color>(heapTexData.Pixels);
            
            //Create new sprite
            Sprite newSprite = new Sprite(null, null);
            sprite.CloneInto(newSprite);
            foreach(var anim in newSprite.Animations) {
                anim.Value.Frames = Enumerable.Range(0, anim.Value.Frames.Length).Select(idx => {
                    MTexture oTex = anim.Value.Frames[idx];
                    MTexture nTex = new MTexture(heapTex, oTex.AtlasPath, animFrames[anim.Value][idx], oTex.DrawOffset, oTex.Width, oTex.Height) {
                        Atlas = oTex.Atlas,
                        ScaleFix = oTex.ScaleFix
                    };
                    return nTex;
                }).ToArray();
            }
            return newSprite;
        }
    }

    public static class AreaHelper {
        public static AreaKey ParseAreaKey(this string str) {
            string sid = str;
            AreaMode mode = AreaMode.Normal;

            int hashTagIndex = str.LastIndexOf('#');
            if(hashTagIndex >= 0 && hashTagIndex == str.Length-2) {
                switch(char.ToLower(str[hashTagIndex+1])) {
                    case 'a': str = str.Substring(hashTagIndex); mode = AreaMode.Normal; break;
                    case 'b': str = str.Substring(hashTagIndex); mode = AreaMode.BSide; break;
                    case 'c': str = str.Substring(hashTagIndex); mode = AreaMode.CSide; break;
                }
            }

            return new AreaKey() { SID = sid, Mode = mode };
        } 
    }
}