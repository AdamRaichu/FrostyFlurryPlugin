// Originally written by wannkunstbeikor
// Ported to v1.0.6.3 API with roundtrip XML serialization support.

using Flurry.Editor.SourceControl;
using Frosty.Core;
using FrostySdk;
using FrostySdk.Attributes;
using FrostySdk.Ebx;
using FrostySdk.IO;
using FrostySdk.Managers;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;

namespace Flurry.Editor
{
    public sealed class DbxWriter : IDisposable
    {
        private static AccessTools.FieldRef<EbxAsset, List<int>> s_refCountsField = AccessTools.FieldRefAccess<EbxAsset, List<int>>("refCounts");

        private static readonly XmlWriterSettings s_settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "\t",
            NewLineChars = "\n"
        };

        private XmlWriter m_xmlWriter;

        public DbxWriter(Stream inStream)
        {
            m_xmlWriter = XmlWriter.Create(inStream, s_settings);
        }

        public DbxWriter(string inFilePath)
        {
            m_xmlWriter = XmlWriter.Create(inFilePath, s_settings);
        }

        public void Write(EbxAsset asset)
        {
            if (!asset.IsValid)
                return;

            WriteAsset(asset);
        }

        public void Dispose()
        {
            m_xmlWriter?.Dispose();
        }

        #region Asset/Partition Writing

        private void WriteAsset(EbxAsset asset)
        {
            m_xmlWriter.WriteStartDocument();
            m_xmlWriter.WriteStartElement("partition");
            m_xmlWriter.WriteAttributeString("guid", asset.FileGuid.ToString());
            m_xmlWriter.WriteAttributeString("primaryInstance", asset.RootInstanceGuid.ToString());

            object[] objects = asset.Objects.ToArray();
            List<int> refCounts = s_refCountsField(asset);
            for (int i = 0; i < objects.Count(); i++)
            {
                // isRoot criteria from EbxAsset.RootObjects getter.
                WriteInstance(objects[i], refCounts[i] == 0 || i == 0);
            }

            m_xmlWriter.WriteEndElement();
        }

        #endregion

        #region Instance Writing

        public void WriteInstance(object ebxObj, bool isRoot = false)
        {
            Type ebxType = ebxObj.GetType();
            AssetClassGuid guid = GetInstanceGuid(ebxObj);

            m_xmlWriter.WriteStartElement("instance");
            m_xmlWriter.WriteAttributeString("guid", guid.ToString());
            m_xmlWriter.WriteAttributeString("type", ebxType.Name);
            m_xmlWriter.WriteAttributeString("exported", guid.IsExported.ToString());
            m_xmlWriter.WriteAttributeString("root", isRoot.ToString());

            if (ebxType.IsClass)
            {
                WriteDbxClass(ebxType, ebxObj);
            }

            m_xmlWriter.WriteEndElement();
        }

        #endregion

        #region Field Writing

        public void WriteField(EbxFieldType fieldType, object obj, Type objType, Type baseType,
            string fieldName = null, bool isArrayItem = false, bool isTransient = false, bool isHidden = false)
        {
            switch (fieldType)
            {
                case EbxFieldType.Boolean:
                    WriteFieldValue(fieldName, (bool)obj, isArrayItem, isTransient, isHidden);
                    break;
                case EbxFieldType.Int8:
                    WriteFieldValue(fieldName, (sbyte)obj, isArrayItem, isTransient, isHidden);
                    break;
                case EbxFieldType.Int16:
                    WriteFieldValue(fieldName, (short)obj, isArrayItem, isTransient, isHidden);
                    break;
                case EbxFieldType.Int32:
                    WriteFieldValue(fieldName, (int)obj, isArrayItem, isTransient, isHidden);
                    break;
                case EbxFieldType.Int64:
                    WriteFieldValue(fieldName, (long)obj, isArrayItem, isTransient, isHidden);
                    break;
                case EbxFieldType.UInt8:
                    WriteFieldValue(fieldName, (byte)obj, isArrayItem, isTransient, isHidden);
                    break;
                case EbxFieldType.UInt16:
                    WriteFieldValue(fieldName, (ushort)obj, isArrayItem, isTransient, isHidden);
                    break;
                case EbxFieldType.UInt32:
                    WriteFieldValue(fieldName, (uint)obj, isArrayItem, isTransient, isHidden);
                    break;
                case EbxFieldType.UInt64:
                    WriteFieldValue(fieldName, (ulong)obj, isArrayItem, isTransient, isHidden);
                    break;
                case EbxFieldType.Float32:
                    WriteFieldValue(fieldName, (float)obj, isArrayItem, isTransient, isHidden);
                    break;
                case EbxFieldType.Float64:
                    WriteFieldValue(fieldName, (double)obj, isArrayItem, isTransient, isHidden);
                    break;
                case EbxFieldType.String:
                    WriteFieldValue(fieldName, ((CString)obj).ToString() ?? "", isArrayItem, isTransient, isHidden);
                    break;
                case EbxFieldType.CString:
                    WriteFieldValue(fieldName, ((CString)obj).ToString() ?? "", isArrayItem, isTransient, isHidden);
                    break;
                case EbxFieldType.Guid:
                    WriteFieldValue(fieldName, ((Guid)obj).ToString("D"), isArrayItem, isTransient, isHidden);
                    break;
                case EbxFieldType.Enum:
                    WriteFieldValue(fieldName, Enum.GetName(objType, obj), isArrayItem, isTransient, isHidden);
                    break;
                case EbxFieldType.ResourceRef:
                    WriteResourceRef(fieldName, (ResourceRef)obj, isArrayItem, isTransient, isHidden);
                    break;
                case EbxFieldType.Sha1:
                    WriteFieldValue(fieldName, ((Sha1)obj).ToString(), isArrayItem, isTransient, isHidden);
                    break;
                case EbxFieldType.Pointer:
                    WritePointerRef(fieldName, (PointerRef)obj, isArrayItem, isTransient, isHidden);
                    break;
                case EbxFieldType.Array:
                    WriteArray(fieldName, obj, objType, baseType);
                    break;
                case EbxFieldType.Struct:
                    WriteStruct(fieldName, obj, objType, isArrayItem);
                    break;
                case EbxFieldType.FileRef:
                    WriteFieldValue(fieldName, ((FileRef)obj).ToString() ?? "", isArrayItem, isTransient, isHidden);
                    break;
                case EbxFieldType.TypeRef:
                    WriteTypeRef(fieldName, (TypeRef)obj);
                    break;
                case EbxFieldType.Delegate:
                    WriteDelegate(fieldName, obj);
                    break;
                case EbxFieldType.BoxedValueRef:
                    WriteBoxedValueRef(fieldName, (BoxedValueRef)obj);
                    break;
            }
        }

        public void WriteFieldStart(string name, bool isArrayField, bool isTransient, bool isHidden)
        {
            m_xmlWriter.WriteStartElement(isArrayField ? "item" : "field");
            if (!isArrayField)
                m_xmlWriter.WriteAttributeString("name", name);
            if (isTransient)
                m_xmlWriter.WriteAttributeString("transient", isTransient.ToString());
            if (isHidden)
                m_xmlWriter.WriteAttributeString("hidden", isHidden.ToString());
        }

        #endregion

        #region Simple Value Writing

        public void WriteFieldValue(string fieldName, object value, bool isArrayField = false, bool isTransient = false, bool isHidden = false)
        {
            if (fieldName != null)
                WriteFieldStart(fieldName, isArrayField, isTransient, isHidden);

            string strVal;
            if (value is float f)
                strVal = f.ToString("0.0######");
            else if (value is double d)
                strVal = d.ToString("0.0##############");
            else if (value is ulong u)
                strVal = u.ToString();
            else if (value is bool b)
                strVal = b.ToString();
            else if (value is string s)
                strVal = s?.Replace("\v", string.Empty) ?? "";
            else
                strVal = value?.ToString() ?? "";

            m_xmlWriter.WriteValue(strVal);

            if (fieldName != null)
                m_xmlWriter.WriteEndElement();
        }

        public void WriteResourceRef(string fieldName, ResourceRef value, bool isArrayField = false, bool isTransient = false, bool isHidden = false)
        {
            if (fieldName != null)
                WriteFieldStart(fieldName, isArrayField, isTransient, isHidden);

            m_xmlWriter.WriteValue(((ulong)value).ToString("X"));

            if (fieldName != null)
                m_xmlWriter.WriteEndElement();
        }

        #endregion

        #region PointerRef Writing

        public void WritePointerRef(string fieldName, PointerRef value, bool isArrayField = false, bool isTransient = false, bool isHidden = false)
        {
            if (fieldName != null)
                WriteFieldStart(fieldName, isArrayField, isTransient, isHidden);

            if (value.Type == PointerRefType.Internal)
            {
                AssetClassGuid classGuid = GetInstanceGuid(value.Internal);
                m_xmlWriter.WriteAttributeString("ref", classGuid.ToString());
            }
            else if (value.Type == PointerRefType.External)
            {
                EbxAssetEntry entry = App.AssetManager.GetEbxEntry(value.External.FileGuid);
                if (entry != null)
                {
                    m_xmlWriter.WriteAttributeString("ref", entry.Name + "\\" + value.External.ClassGuid);
                    m_xmlWriter.WriteAttributeString("partitionGuid", entry.Guid.ToString());
                }
                else
                {
                    m_xmlWriter.WriteAttributeString("ref", "null");
                    m_xmlWriter.WriteAttributeString("partitionGuid", "null");
                }
            }
            else
            {
                m_xmlWriter.WriteAttributeString("ref", "null");
            }

            if (fieldName != null)
                m_xmlWriter.WriteEndElement();
        }

        #endregion

        #region Array Writing

        public void WriteArray(string arrayName, object arrayObj, Type arrayType, Type baseType)
        {
            Type elementType;
            bool isRef = false;

            if (arrayType.IsGenericType)
            {
                Type genArg = arrayType.GetGenericArguments()[0];
                isRef = genArg == typeof(PointerRef);
                elementType = isRef ? baseType : genArg;
            }
            else
            {
                elementType = baseType ?? typeof(object);
            }

            EbxFieldMetaAttribute memberMeta = elementType?.GetCustomAttribute<EbxFieldMetaAttribute>();
            string typeDisplayName = elementType?.Name ?? "unknown";

            m_xmlWriter.WriteStartElement("array");
            if (arrayName != null)
                m_xmlWriter.WriteAttributeString("name", arrayName);
            m_xmlWriter.WriteAttributeString("type", isRef ? "ref(" + typeDisplayName + ")" : typeDisplayName);

            IList list = (IList)arrayObj;
            for (int i = 0; i < list.Count; i++)
            {
                object subValue = list[i];
                EbxFieldType memberType;
                if (isRef)
                    memberType = EbxFieldType.Pointer;
                else if (memberMeta != null)
                    memberType = memberMeta.Type;
                else
                    memberType = InferFieldType(subValue);

                WriteField(memberType, subValue, subValue.GetType(), null, string.Empty, true);
            }

            m_xmlWriter.WriteEndElement();
        }

        #endregion

        #region Struct Writing

        public void WriteStruct(string structName, object structObj, Type structType, bool isArrayItem = false)
        {
            foreach (DbxConversionTransformer transformer in DbxConversionTransformer.Extensions)
            {
                if (transformer.IsTypeSupported(structType))
                {
                    transformer.WriteToDbx(this, m_xmlWriter, structName, structObj, isArrayItem);
                    return;
                }
            }

            m_xmlWriter.WriteStartElement("complex");
            if (!isArrayItem)
            {
                m_xmlWriter.WriteAttributeString("type", structObj.GetType().Name);
                if (structName != null)
                    m_xmlWriter.WriteAttributeString("name", structName);
            }

            List<PropertyInfo> properties = new List<PropertyInfo>();
            GetAllProperties(structType, ref properties);

            foreach (PropertyInfo pi in properties)
            {
                EbxFieldMetaAttribute fieldMeta = pi.GetCustomAttribute<EbxFieldMetaAttribute>();
                if (fieldMeta == null) continue;

                WriteField(fieldMeta.Type,
                    pi.GetValue(structObj),
                    pi.PropertyType,
                    fieldMeta.BaseType,
                    pi.Name,
                    false,
                    pi.GetCustomAttribute<IsTransientAttribute>() != null,
                    pi.GetCustomAttribute<IsHiddenAttribute>() != null);
            }

            m_xmlWriter.WriteEndElement();
        }

        #endregion

        #region BoxedValueRef Writing

        public void WriteBoxedValueRef(string name, BoxedValueRef boxedValue)
        {
            m_xmlWriter.WriteStartElement("boxed");
            m_xmlWriter.WriteAttributeString("name", name);

            if (boxedValue.Value != null)
            {
                string typeName = boxedValue.Value.GetType().Name;
                m_xmlWriter.WriteAttributeString("typeName", typeName);
                WriteField(boxedValue.Type, boxedValue.Value, boxedValue.Value.GetType(), null);
            }

            m_xmlWriter.WriteEndElement();
        }

        #endregion

        #region TypeRef Writing

        public void WriteTypeRef(string name, TypeRef typeRef)
        {
            m_xmlWriter.WriteStartElement("typeref");
            m_xmlWriter.WriteAttributeString("name", name);

            if (!typeRef.IsNull())
                m_xmlWriter.WriteAttributeString("typeName", typeRef.Name);

            m_xmlWriter.WriteEndElement();
        }

        #endregion

        #region Delegate Writing

        public void WriteDelegate(string name, object delegateObj)
        {
            m_xmlWriter.WriteStartElement("delegate");
            m_xmlWriter.WriteAttributeString("name", name);

            Type objType = delegateObj?.GetType();
            if (objType != null)
            {
                PropertyInfo funcTypeProp = objType.GetProperty("FunctionType");
                if (funcTypeProp != null)
                {
                    object funcType = funcTypeProp.GetValue(delegateObj);
                    if (funcType != null)
                    {
                        PropertyInfo nameProp = funcType.GetType().GetProperty("Name");
                        if (nameProp != null)
                            m_xmlWriter.WriteAttributeString("typeName", nameProp.GetValue(funcType)?.ToString());
                    }
                }
            }

            m_xmlWriter.WriteEndElement();
        }

        #endregion

        #region Class Writing

        public void WriteDbxClass(Type classType, object classObj)
        {
            List<PropertyInfo> properties = new List<PropertyInfo>();
            GetAllProperties(classType, ref properties, true, true);

            foreach (PropertyInfo pi in properties)
            {
                EbxFieldMetaAttribute fieldMeta = pi.GetCustomAttribute<EbxFieldMetaAttribute>();
                if (fieldMeta == null) continue;

                WriteField(fieldMeta.Type,
                    pi.GetValue(classObj),
                    pi.PropertyType,
                    fieldMeta.BaseType,
                    pi.Name,
                    false,
                    pi.GetCustomAttribute<IsTransientAttribute>() != null,
                    pi.GetCustomAttribute<IsHiddenAttribute>() != null);
            }
        }

        #endregion

        #region Helpers

        public static AssetClassGuid GetInstanceGuid(object obj)
        {
            FieldInfo fi = obj.GetType().GetField("__Guid", BindingFlags.NonPublic | BindingFlags.Instance);
            if (fi != null)
                return (AssetClassGuid)fi.GetValue(obj);

            return ((dynamic)obj).GetInstanceGuid();
        }

        public void GetAllProperties(Type classType, ref List<PropertyInfo> properties, bool checkBaseTypes = false, bool shouldSort = false)
        {
            PropertyInfo[] currentTypeProps = classType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            foreach (PropertyInfo pi in currentTypeProps)
            {
                if (pi.Name == "__InstanceGuid" || pi.Name == "__Id")
                    continue;

                properties.Add(pi);
            }

            if (checkBaseTypes)
            {
                Type baseType = classType.BaseType;
                if (baseType != null && baseType != typeof(object))
                    GetAllProperties(baseType, ref properties, checkBaseTypes, shouldSort);
            }

            if (shouldSort)
            {
                properties.Sort((p1, p2) =>
                {
                    int index1 = p1.GetCustomAttribute<FieldIndexAttribute>()?.Index ?? -1;
                    int index2 = p2.GetCustomAttribute<FieldIndexAttribute>()?.Index ?? -1;
                    return index1.CompareTo(index2);
                });
            }
        }

        public static EbxFieldType InferFieldType(object value)
        {
            if (value == null) return EbxFieldType.Struct;

            Type t = value.GetType();
            if (t == typeof(bool)) return EbxFieldType.Boolean;
            if (t == typeof(sbyte)) return EbxFieldType.Int8;
            if (t == typeof(short)) return EbxFieldType.Int16;
            if (t == typeof(int)) return EbxFieldType.Int32;
            if (t == typeof(long)) return EbxFieldType.Int64;
            if (t == typeof(byte)) return EbxFieldType.UInt8;
            if (t == typeof(ushort)) return EbxFieldType.UInt16;
            if (t == typeof(uint)) return EbxFieldType.UInt32;
            if (t == typeof(ulong)) return EbxFieldType.UInt64;
            if (t == typeof(float)) return EbxFieldType.Float32;
            if (t == typeof(double)) return EbxFieldType.Float64;
            if (t == typeof(CString)) return EbxFieldType.CString;
            if (t == typeof(Guid)) return EbxFieldType.Guid;
            if (t == typeof(ResourceRef)) return EbxFieldType.ResourceRef;
            if (t == typeof(Sha1)) return EbxFieldType.Sha1;
            if (t == typeof(PointerRef)) return EbxFieldType.Pointer;
            if (t == typeof(FileRef)) return EbxFieldType.FileRef;
            if (t == typeof(TypeRef)) return EbxFieldType.TypeRef;
            if (t == typeof(BoxedValueRef)) return EbxFieldType.BoxedValueRef;
            if (t.IsEnum) return EbxFieldType.Enum;
            if (t.IsValueType) return EbxFieldType.Struct;
            return EbxFieldType.Struct;
        }

        #endregion
    }
}
