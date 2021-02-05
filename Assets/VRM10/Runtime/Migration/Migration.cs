﻿using System;
using System.Collections.Generic;
using System.Linq;
using UniJSON;

namespace UniVRM10
{
    /// <summary>
    /// Convert vrm0 binary to vrm1 binary. Json processing
    /// </summary>
    public static class Migration
    {
        static bool TryGet(this UniGLTF.glTFExtensionImport extensions, string key, out JsonNode value)
        {
            foreach (var kv in extensions.ObjectItems())
            {
                if (kv.Key.GetString() == key)
                {
                    value = kv.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }




        static UniGLTF.Extensions.VRMC_vrm.ExpressionPreset ToPreset(JsonNode json)
        {
            switch (json.GetString().ToLower())
            {
                case "unknown": return UniGLTF.Extensions.VRMC_vrm.ExpressionPreset.custom;

                // https://github.com/vrm-c/vrm-specification/issues/185
                case "neutral": return UniGLTF.Extensions.VRMC_vrm.ExpressionPreset.neutral;

                case "a": return UniGLTF.Extensions.VRMC_vrm.ExpressionPreset.aa;
                case "i": return UniGLTF.Extensions.VRMC_vrm.ExpressionPreset.ih;
                case "u": return UniGLTF.Extensions.VRMC_vrm.ExpressionPreset.ou;
                case "e": return UniGLTF.Extensions.VRMC_vrm.ExpressionPreset.ee;
                case "o": return UniGLTF.Extensions.VRMC_vrm.ExpressionPreset.oh;

                case "blink": return UniGLTF.Extensions.VRMC_vrm.ExpressionPreset.blink;
                case "blink_l": return UniGLTF.Extensions.VRMC_vrm.ExpressionPreset.blinkLeft;
                case "blink_r": return UniGLTF.Extensions.VRMC_vrm.ExpressionPreset.blinkRight;

                // https://github.com/vrm-c/vrm-specification/issues/163
                case "joy": return UniGLTF.Extensions.VRMC_vrm.ExpressionPreset.happy;
                case "angry": return UniGLTF.Extensions.VRMC_vrm.ExpressionPreset.angry;
                case "sorrow": return UniGLTF.Extensions.VRMC_vrm.ExpressionPreset.sad;
                case "fun": return UniGLTF.Extensions.VRMC_vrm.ExpressionPreset.relaxed;

                case "lookup": return UniGLTF.Extensions.VRMC_vrm.ExpressionPreset.lookUp;
                case "lookdown": return UniGLTF.Extensions.VRMC_vrm.ExpressionPreset.lookDown;
                case "lookleft": return UniGLTF.Extensions.VRMC_vrm.ExpressionPreset.lookLeft;
                case "lookright": return UniGLTF.Extensions.VRMC_vrm.ExpressionPreset.lookRight;
            }

            throw new NotImplementedException();
        }

        static IEnumerable<UniGLTF.Extensions.VRMC_vrm.MorphTargetBind> ToMorphTargetBinds(UniGLTF.glTF gltf, JsonNode json)
        {
            foreach (var x in json.ArrayItems())
            {
                var meshIndex = x["mesh"].GetInt32();
                var morphTargetIndex = x["index"].GetInt32();
                var weight = x["weight"].GetSingle();

                var bind = new UniGLTF.Extensions.VRMC_vrm.MorphTargetBind();

                // https://github.com/vrm-c/vrm-specification/pull/106
                // https://github.com/vrm-c/vrm-specification/pull/153
                bind.Node = gltf.nodes.IndexOf(gltf.nodes.First(y => y.mesh == meshIndex));
                bind.Index = morphTargetIndex;
                // https://github.com/vrm-c/vrm-specification/issues/209                
                bind.Weight = weight * 0.01f;

                yield return bind;
            }
        }

        public const string COLOR_PROPERTY = "_Color";
        public const string EMISSION_COLOR_PROPERTY = "_EmissionColor";
        public const string RIM_COLOR_PROPERTY = "_RimColor";
        public const string OUTLINE_COLOR_PROPERTY = "_OutlineColor";
        public const string SHADE_COLOR_PROPERTY = "_ShadeColor";

        static UniGLTF.Extensions.VRMC_vrm.MaterialColorType ToMaterialType(string src)
        {
            switch (src)
            {
                case COLOR_PROPERTY:
                    return UniGLTF.Extensions.VRMC_vrm.MaterialColorType.color;

                case EMISSION_COLOR_PROPERTY:
                    return UniGLTF.Extensions.VRMC_vrm.MaterialColorType.emissionColor;

                case RIM_COLOR_PROPERTY:
                    return UniGLTF.Extensions.VRMC_vrm.MaterialColorType.rimColor;

                case SHADE_COLOR_PROPERTY:
                    return UniGLTF.Extensions.VRMC_vrm.MaterialColorType.shadeColor;

                case OUTLINE_COLOR_PROPERTY:
                    return UniGLTF.Extensions.VRMC_vrm.MaterialColorType.outlineColor;
            }

            throw new NotImplementedException();
        }

        /// <summary>
        /// MaterialValue の仕様変更
        /// 
        /// * MaterialColorBind
        /// * TextureTransformBind
        /// 
        /// の２種類になった。
        /// 
        /// </summary>
        /// <param name="gltf"></param>
        /// <param name="json"></param>
        /// <param name="expression"></param>
        static void ToMaterialColorBinds(UniGLTF.glTF gltf, JsonNode json, UniGLTF.Extensions.VRMC_vrm.Expression expression)
        {
            foreach (var x in json.ArrayItems())
            {
                var materialName = x["materialName"].GetString();
                var materialIndex = gltf.materials.IndexOf(gltf.materials.First(y => y.name == materialName));
                var propertyName = x["propertyName"].GetString();
                var targetValue = x["targetValue"].ArrayItems().Select(y => y.GetSingle()).ToArray();
                if (propertyName.EndsWith("_ST"))
                {
                    expression.TextureTransformBinds.Add(new UniGLTF.Extensions.VRMC_vrm.TextureTransformBind
                    {
                        Material = materialIndex,
                        Scaling = new float[] { targetValue[0], targetValue[1] },
                        Offset = new float[] { targetValue[2], targetValue[3] }
                    });
                }
                else if (propertyName.EndsWith("_ST_S"))
                {
                    expression.TextureTransformBinds.Add(new UniGLTF.Extensions.VRMC_vrm.TextureTransformBind
                    {
                        Material = materialIndex,
                        Scaling = new float[] { targetValue[0], 1 },
                        Offset = new float[] { targetValue[2], 0 }
                    });
                }
                else if (propertyName.EndsWith("_ST_T"))
                {
                    expression.TextureTransformBinds.Add(new UniGLTF.Extensions.VRMC_vrm.TextureTransformBind
                    {
                        Material = materialIndex,
                        Scaling = new float[] { 1, targetValue[1] },
                        Offset = new float[] { 0, targetValue[3] }
                    });
                }
                else
                {
                    // color
                    expression.MaterialColorBinds.Add(new UniGLTF.Extensions.VRMC_vrm.MaterialColorBind
                    {
                        Material = materialIndex,
                        Type = ToMaterialType(propertyName),
                        TargetValue = targetValue,
                    });
                }
            }
        }

        static IEnumerable<UniGLTF.Extensions.VRMC_vrm.Expression> MigrateExpression(UniGLTF.glTF gltf, JsonNode json)
        {
            foreach (var blendShapeClip in json["blendShapeGroups"].ArrayItems())
            {
                var name = blendShapeClip["name"].GetString();
                var expression = new UniGLTF.Extensions.VRMC_vrm.Expression
                {
                    Name = name,
                    Preset = ToPreset(blendShapeClip["presetName"]),
                    IsBinary = blendShapeClip["isBinary"].GetBoolean(),
                };
                expression.MorphTargetBinds = ToMorphTargetBinds(gltf, blendShapeClip["binds"]).ToList();
                ToMaterialColorBinds(gltf, blendShapeClip["materialValues"], expression);
                yield return expression;
            }
        }

        public static byte[] Migrate(byte[] src)
        {
            var glb = UniGLTF.Glb.Parse(src);
            var json = glb.Json.Bytes.ParseAsJson();
            var gltf = UniGLTF.GltfDeserializer.Deserialize(json);

            var extensions = new UniGLTF.glTFExtensionExport();
            {
                var vrm0 = json["extensions"]["VRM"];

                {
                    // vrm
                    var vrm1 = new UniGLTF.Extensions.VRMC_vrm.VRMC_vrm();
                    vrm1.Meta = MigrationVrmMeta.Migrate(vrm0["meta"]);
                    vrm1.Humanoid = MigrationVrmHumanoid.Migrate(vrm0["humanoid"]);
                    vrm1.Expressions = MigrateExpression(gltf, vrm0["blendShapeMaster"]).ToList();

                    var f = new JsonFormatter();
                    UniGLTF.Extensions.VRMC_vrm.GltfSerializer.Serialize(f, vrm1);
                    extensions.Add(UniGLTF.Extensions.VRMC_vrm.VRMC_vrm.ExtensionName, f.GetStoreBytes());
                }
                {
                    // springBone & collider
                    var vrm1 = MigrationVrmSpringBone.Migrate(gltf, json["extensions"]["VRM"]["secondaryAnimation"]);

                    var f = new JsonFormatter();
                    UniGLTF.Extensions.VRMC_springBone.GltfSerializer.Serialize(f, vrm1);
                    extensions.Add(UniGLTF.Extensions.VRMC_springBone.VRMC_springBone.ExtensionName, f.GetStoreBytes());
                }
                {
                    // MToon
                }
                {
                    // constraint
                }
            }

            ArraySegment<byte> vrm1Json = default;
            {
                gltf.extensions = extensions;

                var f = new JsonFormatter();
                UniGLTF.GltfSerializer.Serialize(f, gltf);
                vrm1Json = f.GetStoreBytes();
            }

            return UniGLTF.Glb.Create(vrm1Json, glb.Binary.Bytes).ToBytes();
        }

        #region for UnitTest
        public class MigrationException : Exception
        {
            public MigrationException(string key, string value) : base($"{key}: {value}")
            {
            }
        }

        public static void CheckBone(string bone, JsonNode vrm0, UniGLTF.Extensions.VRMC_vrm.HumanBone vrm1)
        {
            var vrm0NodeIndex = vrm0["node"].GetInt32();
            if (vrm0NodeIndex != vrm1.Node)
            {
                throw new Exception($"different {bone}: {vrm0NodeIndex} != {vrm1.Node}");
            }
        }

        public static void CheckHumanoid(JsonNode vrm0, UniGLTF.Extensions.VRMC_vrm.Humanoid vrm1)
        {
            foreach (var humanoidBone in vrm0["humanBones"].ArrayItems())
            {
                var boneType = humanoidBone["bone"].GetString();
                switch (boneType)
                {
                    case "hips": CheckBone(boneType, humanoidBone, vrm1.HumanBones.Hips); break;
                    case "leftUpperLeg": CheckBone(boneType, humanoidBone, vrm1.HumanBones.LeftUpperLeg); break;
                    case "rightUpperLeg": CheckBone(boneType, humanoidBone, vrm1.HumanBones.RightUpperLeg); break;
                    case "leftLowerLeg": CheckBone(boneType, humanoidBone, vrm1.HumanBones.LeftLowerLeg); break;
                    case "rightLowerLeg": CheckBone(boneType, humanoidBone, vrm1.HumanBones.RightLowerLeg); break;
                    case "leftFoot": CheckBone(boneType, humanoidBone, vrm1.HumanBones.LeftFoot); break;
                    case "rightFoot": CheckBone(boneType, humanoidBone, vrm1.HumanBones.RightFoot); break;
                    case "spine": CheckBone(boneType, humanoidBone, vrm1.HumanBones.Spine); break;
                    case "chest": CheckBone(boneType, humanoidBone, vrm1.HumanBones.Chest); break;
                    case "neck": CheckBone(boneType, humanoidBone, vrm1.HumanBones.Neck); break;
                    case "head": CheckBone(boneType, humanoidBone, vrm1.HumanBones.Head); break;
                    case "leftShoulder": CheckBone(boneType, humanoidBone, vrm1.HumanBones.LeftShoulder); break;
                    case "rightShoulder": CheckBone(boneType, humanoidBone, vrm1.HumanBones.RightShoulder); break;
                    case "leftUpperArm": CheckBone(boneType, humanoidBone, vrm1.HumanBones.LeftUpperArm); break;
                    case "rightUpperArm": CheckBone(boneType, humanoidBone, vrm1.HumanBones.RightUpperArm); break;
                    case "leftLowerArm": CheckBone(boneType, humanoidBone, vrm1.HumanBones.LeftLowerArm); break;
                    case "rightLowerArm": CheckBone(boneType, humanoidBone, vrm1.HumanBones.RightLowerArm); break;
                    case "leftHand": CheckBone(boneType, humanoidBone, vrm1.HumanBones.LeftHand); break;
                    case "rightHand": CheckBone(boneType, humanoidBone, vrm1.HumanBones.RightHand); break;
                    case "leftToes": CheckBone(boneType, humanoidBone, vrm1.HumanBones.LeftToes); break;
                    case "rightToes": CheckBone(boneType, humanoidBone, vrm1.HumanBones.RightToes); break;
                    case "leftEye": CheckBone(boneType, humanoidBone, vrm1.HumanBones.LeftEye); break;
                    case "rightEye": CheckBone(boneType, humanoidBone, vrm1.HumanBones.RightEye); break;
                    case "jaw": CheckBone(boneType, humanoidBone, vrm1.HumanBones.Jaw); break;
                    case "leftThumbProximal": CheckBone(boneType, humanoidBone, vrm1.HumanBones.LeftThumbProximal); break;
                    case "leftThumbIntermediate": CheckBone(boneType, humanoidBone, vrm1.HumanBones.LeftThumbIntermediate); break;
                    case "leftThumbDistal": CheckBone(boneType, humanoidBone, vrm1.HumanBones.LeftThumbDistal); break;
                    case "leftIndexProximal": CheckBone(boneType, humanoidBone, vrm1.HumanBones.LeftIndexProximal); break;
                    case "leftIndexIntermediate": CheckBone(boneType, humanoidBone, vrm1.HumanBones.LeftIndexIntermediate); break;
                    case "leftIndexDistal": CheckBone(boneType, humanoidBone, vrm1.HumanBones.LeftIndexDistal); break;
                    case "leftMiddleProximal": CheckBone(boneType, humanoidBone, vrm1.HumanBones.LeftMiddleProximal); break;
                    case "leftMiddleIntermediate": CheckBone(boneType, humanoidBone, vrm1.HumanBones.LeftMiddleIntermediate); break;
                    case "leftMiddleDistal": CheckBone(boneType, humanoidBone, vrm1.HumanBones.LeftMiddleDistal); break;
                    case "leftRingProximal": CheckBone(boneType, humanoidBone, vrm1.HumanBones.LeftRingProximal); break;
                    case "leftRingIntermediate": CheckBone(boneType, humanoidBone, vrm1.HumanBones.LeftRingIntermediate); break;
                    case "leftRingDistal": CheckBone(boneType, humanoidBone, vrm1.HumanBones.LeftRingDistal); break;
                    case "leftLittleProximal": CheckBone(boneType, humanoidBone, vrm1.HumanBones.LeftLittleProximal); break;
                    case "leftLittleIntermediate": CheckBone(boneType, humanoidBone, vrm1.HumanBones.LeftLittleIntermediate); break;
                    case "leftLittleDistal": CheckBone(boneType, humanoidBone, vrm1.HumanBones.LeftLittleDistal); break;
                    case "rightThumbProximal": CheckBone(boneType, humanoidBone, vrm1.HumanBones.RightThumbProximal); break;
                    case "rightThumbIntermediate": CheckBone(boneType, humanoidBone, vrm1.HumanBones.RightThumbIntermediate); break;
                    case "rightThumbDistal": CheckBone(boneType, humanoidBone, vrm1.HumanBones.RightThumbDistal); break;
                    case "rightIndexProximal": CheckBone(boneType, humanoidBone, vrm1.HumanBones.RightIndexProximal); break;
                    case "rightIndexIntermediate": CheckBone(boneType, humanoidBone, vrm1.HumanBones.RightIndexIntermediate); break;
                    case "rightIndexDistal": CheckBone(boneType, humanoidBone, vrm1.HumanBones.RightIndexDistal); break;
                    case "rightMiddleProximal": CheckBone(boneType, humanoidBone, vrm1.HumanBones.RightMiddleProximal); break;
                    case "rightMiddleIntermediate": CheckBone(boneType, humanoidBone, vrm1.HumanBones.RightMiddleIntermediate); break;
                    case "rightMiddleDistal": CheckBone(boneType, humanoidBone, vrm1.HumanBones.RightMiddleDistal); break;
                    case "rightRingProximal": CheckBone(boneType, humanoidBone, vrm1.HumanBones.RightRingProximal); break;
                    case "rightRingIntermediate": CheckBone(boneType, humanoidBone, vrm1.HumanBones.RightRingIntermediate); break;
                    case "rightRingDistal": CheckBone(boneType, humanoidBone, vrm1.HumanBones.RightRingDistal); break;
                    case "rightLittleProximal": CheckBone(boneType, humanoidBone, vrm1.HumanBones.RightLittleProximal); break;
                    case "rightLittleIntermediate": CheckBone(boneType, humanoidBone, vrm1.HumanBones.RightLittleIntermediate); break;
                    case "rightLittleDistal": CheckBone(boneType, humanoidBone, vrm1.HumanBones.RightLittleDistal); break;
                    case "upperChest": CheckBone(boneType, humanoidBone, vrm1.HumanBones.UpperChest); break;
                    default: throw new MigrationException("humanonoid.humanBones[*].bone", boneType);
                }
            }
        }

        static bool IsSingleList(string key, string lhs, List<string> rhs)
        {
            if (rhs.Count != 1) throw new MigrationException(key, $"{rhs.Count}");
            return lhs == rhs[0];
        }

        static string AvatarPermission(string key, UniGLTF.Extensions.VRMC_vrm.AvatarPermissionType x)
        {
            switch (x)
            {
                case UniGLTF.Extensions.VRMC_vrm.AvatarPermissionType.everyone: return "Everyone";
                    // case AvatarPermissionType.onlyAuthor: return "OnlyAuthor";
                    // case AvatarPermissionType.explicitlyLicensedPerson: return "Explicited";
            }
            throw new MigrationException(key, $"{x}");
        }

        public static void CheckMeta(JsonNode vrm0, UniGLTF.Extensions.VRMC_vrm.Meta vrm1)
        {
            if (vrm0["title"].GetString() != vrm1.Name) throw new MigrationException("meta.title", vrm1.Name);
            if (vrm0["version"].GetString() != vrm1.Version) throw new MigrationException("meta.version", vrm1.Version);
            if (!IsSingleList("meta.author", vrm0["author"].GetString(), vrm1.Authors)) throw new MigrationException("meta.author", $"{vrm1.Authors}");
            if (vrm0["contactInformation"].GetString() != vrm1.ContactInformation) throw new MigrationException("meta.contactInformation", vrm1.ContactInformation);
            if (!IsSingleList("meta.reference", vrm0["reference"].GetString(), vrm1.References)) throw new MigrationException("meta.reference", $"{vrm1.References}");
            if (vrm0["texture"].GetInt32() != vrm1.ThumbnailImage) throw new MigrationException("meta.texture", $"{vrm1.ThumbnailImage}");

            if (vrm0["allowedUserName"].GetString() != AvatarPermission("meta.allowedUserName", vrm1.AvatarPermission)) throw new MigrationException("meta.allowedUserName", $"{vrm1.AvatarPermission}");
            if (vrm0["violentUssageName"].GetString() == "Allow" != vrm1.AllowExcessivelyViolentUsage) throw new MigrationException("meta.violentUssageName", $"{vrm1.AllowExcessivelyViolentUsage}");
            if (vrm0["sexualUssageName"].GetString() == "Allow" != vrm1.AllowExcessivelySexualUsage) throw new MigrationException("meta.sexualUssageName", $"{vrm1.AllowExcessivelyViolentUsage}");

            if (vrm0["commercialUssageName"].GetString() == "Allow")
            {
                if (vrm1.CommercialUsage == UniGLTF.Extensions.VRMC_vrm.CommercialUsageType.personalNonProfit)
                {
                    throw new MigrationException("meta.commercialUssageName", $"{vrm1.CommercialUsage}");
                }
            }
            else
            {
                if (vrm1.CommercialUsage == UniGLTF.Extensions.VRMC_vrm.CommercialUsageType.corporation
                || vrm1.CommercialUsage == UniGLTF.Extensions.VRMC_vrm.CommercialUsageType.personalProfit)
                {
                    throw new MigrationException("meta.commercialUssageName", $"{vrm1.CommercialUsage}");
                }
            }

            if (MigrationVrmMeta.GetLicenseUrl(vrm0) != vrm1.OtherLicenseUrl) throw new MigrationException("meta.otherLicenseUrl", vrm1.OtherLicenseUrl);

            switch (vrm0["licenseName"].GetString())
            {
                case "Other":
                    {
                        if (vrm1.Modification != UniGLTF.Extensions.VRMC_vrm.ModificationType.prohibited) throw new MigrationException("meta.licenceName", $"{vrm1.Modification}");
                        if (vrm1.AllowRedistribution.Value) throw new MigrationException("meta.liceneName", $"{vrm1.Modification}");
                        break;
                    }

                default:
                    throw new NotImplementedException();
            }
        }

        public static void Check(JsonNode vrm0, UniGLTF.Extensions.VRMC_vrm.VRMC_vrm vrm1)
        {
            Migration.CheckMeta(vrm0["meta"], vrm1.Meta);
            Migration.CheckHumanoid(vrm0["humanoid"], vrm1.Humanoid);
        }

        public static void Check(JsonNode vrm0, UniGLTF.Extensions.VRMC_springBone.VRMC_springBone vrm1, List<UniGLTF.glTFNode> nodes)
        {
            // Migration.CheckSpringBone(vrm0["secondaryAnimation"], vrm1.sp)
        }
        #endregion
    }
}
