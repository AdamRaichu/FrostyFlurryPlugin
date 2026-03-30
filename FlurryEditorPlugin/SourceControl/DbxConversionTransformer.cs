using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using SharpDX;
using Frosty.Core.Viewport;
using FrostySdk;

namespace Flurry.Editor.SourceControl
{
    public abstract class DbxConversionTransformer
    {
        private static readonly List<DbxConversionTransformer> s_extensions = new List<DbxConversionTransformer>();
        public static IEnumerable<DbxConversionTransformer> Extensions => s_extensions;

        static DbxConversionTransformer()
        {
            foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
            {
                if (!type.IsAbstract && type.IsSubclassOf(typeof(DbxConversionTransformer)))
                {
                    s_extensions.Add((DbxConversionTransformer)Activator.CreateInstance(type));
                }
            }
        }

        protected virtual HashSet<string> SupportedTypeNames { get; } = new HashSet<string>();

        public virtual bool IsTypeSupported(Type type) { 
            return SupportedTypeNames.Contains(type.Name);
        }

        public abstract void WriteToDbx(DbxWriter dbxWriter, XmlWriter xmlWriter, string structName, object structObj, bool isArrayItem = false);
        public abstract object ReadFromDbx(DbxReader dbxReader, XmlNode node);
    }

    public class LinearTransformDbxTransformer : DbxConversionTransformer
    {
        protected override HashSet<string> SupportedTypeNames => new HashSet<string>() {
            "LinearTransform"
        };

        public override object ReadFromDbx(DbxReader dbxReader, XmlNode node)
        {
            Type type = TypeLibrary.GetType("LinearTransform");
            dynamic obj = Activator.CreateInstance(type);

            bool isCustomFormat = false;
            Vector3 translation = new Vector3();
            Vector3 rotation = new Vector3();
            Vector3 scale = new Vector3(1, 1, 1);
            
            // Legacy fallbacks
            Vector3 right = new Vector3();
            Vector3 up = new Vector3();
            Vector3 forward = new Vector3();
            Vector3 trans = new Vector3();
            Vector3 legacyTranslate = new Vector3();

            foreach (XmlNode child in node.ChildNodes)
            {
                if (child.Name == "complex" && child.Attributes?["type"]?.Value == "Vec3")
                {
                    string name = child.Attributes?["name"]?.Value;
                    
                    float x = 0, y = 0, z = 0;
                    foreach (XmlNode field in child.ChildNodes)
                    {
                        if (field.Name == "field")
                        {
                            string fieldName = field.Attributes?["name"]?.Value;
                            if (fieldName == "x") x = float.Parse(field.InnerText, CultureInfo.InvariantCulture);
                            if (fieldName == "y") y = float.Parse(field.InnerText, CultureInfo.InvariantCulture);
                            if (fieldName == "z") z = float.Parse(field.InnerText, CultureInfo.InvariantCulture);
                        }
                    }

                    if (name == "Translation") { translation = new Vector3(x, y, z); isCustomFormat = true; }
                    else if (name == "Rotation") rotation = new Vector3(x, y, z);
                    else if (name == "Scale") scale = new Vector3(x, y, z);
                    else if (name == "right") right = new Vector3(x, y, z);
                    else if (name == "up") up = new Vector3(x, y, z);
                    else if (name == "forward") forward = new Vector3(x, y, z);
                    else if (name == "trans") trans = new Vector3(x, y, z);
                    else if (name == "Translate") legacyTranslate = new Vector3(x, y, z);
                }
            }

            if (isCustomFormat)
            {
                float val = (float)(Math.PI / 180.0);
                Matrix m = Matrix.RotationX(rotation.X * val) * Matrix.RotationY(rotation.Y * val) * Matrix.RotationZ(rotation.Z * val);
                m = m * Matrix.Scaling(scale.X, scale.Y, scale.Z);

                right = new Vector3(m.M11, m.M12, m.M13);
                up = new Vector3(m.M21, m.M22, m.M23);
                forward = new Vector3(m.M31, m.M32, m.M33);
                trans = translation;
                legacyTranslate = translation;
            }

            dynamic translateObj = TypeLibrary.CreateObject("Vec3");
            translateObj.x = legacyTranslate.X;
            translateObj.y = legacyTranslate.Y;
            translateObj.z = legacyTranslate.Z;
            obj.Translate = translateObj;

            dynamic rotationObj = TypeLibrary.CreateObject("Vec3");
            rotationObj.x = rotation.X;
            rotationObj.y = rotation.Y;
            rotationObj.z = rotation.Z;
            obj.Rotation = rotationObj;

            dynamic scaleObj = TypeLibrary.CreateObject("Vec3");
            scaleObj.x = scale.X;
            scaleObj.y = scale.Y;
            scaleObj.z = scale.Z;
            obj.Scale = scaleObj;

            dynamic transObj = TypeLibrary.CreateObject("Vec3");
            transObj.x = trans.X;
            transObj.y = trans.Y;
            transObj.z = trans.Z;
            obj.trans = transObj;

            dynamic rightObj = TypeLibrary.CreateObject("Vec3");
            rightObj.x = right.X;
            rightObj.y = right.Y;
            rightObj.z = right.Z;
            obj.right = rightObj;

            dynamic upObj = TypeLibrary.CreateObject("Vec3");
            upObj.x = up.X;
            upObj.y = up.Y;
            upObj.z = up.Z;
            obj.up = upObj;

            dynamic forwardObj = TypeLibrary.CreateObject("Vec3");
            forwardObj.x = forward.X;
            forwardObj.y = forward.Y;
            forwardObj.z = forward.Z;
            obj.forward = forwardObj;

            return obj;
        }

        public override void WriteToDbx(DbxWriter dbxWriter, XmlWriter xmlWriter, string structName, object structObj, bool isArrayItem = false)
        {
            dynamic obj = structObj;

            Vector3 translation = new Vector3();
            Vector3 rotation = new Vector3();
            Vector3 scale = new Vector3();

            float rotX = (float)obj.Rotation.x;
            if (rotX >= 3.4E+38f)
            {
                Matrix matrix = new Matrix(
                        (float)obj.right.x, (float)obj.right.y, (float)obj.right.z, 0.0f,
                        (float)obj.up.x, (float)obj.up.y, (float)obj.up.z, 0.0f,
                        (float)obj.forward.x, (float)obj.forward.y, (float)obj.forward.z, 0.0f,
                        (float)obj.trans.x, (float)obj.trans.y, (float)obj.trans.z, 1.0f
                        );

                matrix.Decompose(out Vector3 scaleVec, out Quaternion quatRotation, out Vector3 transVec);
                Vector3 euler = SharpDXUtils.ExtractEulerAngles(matrix);

                translation = transVec;
                scale = scaleVec;
                rotation = euler;
            }
            else
            {
                if (rotX > 10000.0f || rotX < -10000.0f)
                {
                    Frosty.Core.App.Logger.LogWarning("SourceControl: Unexpectedly large Rotation.x ({0}) found in {1} that is not float.MaxValue. Serializing as Euler mode.", rotX, structName);
                }

                translation = new Vector3((float)obj.Translate.x, (float)obj.Translate.y, (float)obj.Translate.z);
                rotation = new Vector3((float)obj.Rotation.x, (float)obj.Rotation.y, (float)obj.Rotation.z);
                scale = new Vector3((float)obj.Scale.x, (float)obj.Scale.y, (float)obj.Scale.z);
            }

            xmlWriter.WriteStartElement("complex");
            if (!isArrayItem)
            {
                xmlWriter.WriteAttributeString("type", structObj.GetType().Name);
                if (structName != null)
                    xmlWriter.WriteAttributeString("name", structName);
            }

            WriteVec3ToXml(xmlWriter, "Translation", translation);
            WriteVec3ToXml(xmlWriter, "Rotation", rotation);
            WriteVec3ToXml(xmlWriter, "Scale", scale);

            xmlWriter.WriteEndElement();
        }

        private void WriteVec3ToXml(XmlWriter xmlWriter, string name, Vector3 vec)
        {
            xmlWriter.WriteStartElement("complex");
            xmlWriter.WriteAttributeString("type", "Vec3");
            xmlWriter.WriteAttributeString("name", name);

            xmlWriter.WriteStartElement("field");
            xmlWriter.WriteAttributeString("name", "x");
            xmlWriter.WriteValue(vec.X.ToString("0.0######", CultureInfo.InvariantCulture));
            xmlWriter.WriteEndElement();

            xmlWriter.WriteStartElement("field");
            xmlWriter.WriteAttributeString("name", "y");
            xmlWriter.WriteValue(vec.Y.ToString("0.0######", CultureInfo.InvariantCulture));
            xmlWriter.WriteEndElement();

            xmlWriter.WriteStartElement("field");
            xmlWriter.WriteAttributeString("name", "z");
            xmlWriter.WriteValue(vec.Z.ToString("0.0######", CultureInfo.InvariantCulture));
            xmlWriter.WriteEndElement();

            xmlWriter.WriteEndElement();
        }
    }
}
