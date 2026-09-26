using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace RoguePirates.V4.Config
{
    /// <summary>
    /// Field paths inside a config object: <c>field</c>, <c>nested.field</c>, <c>list[3].field</c>. Reading and
    /// writing work through nested classes, structs (written back up the chain) and lists.
    /// </summary>
    public static class ConfigPath
    {
        readonly struct Segment
        {
            public readonly string Name;
            public readonly int Index; // -1 when the segment is a plain field
            public Segment(string name, int index) { Name = name; Index = index; }
        }

        static List<Segment> Parse(string path)
        {
            var result = new List<Segment>();
            foreach (var part in path.Split('.'))
            {
                var name = part;
                while (true)
                {
                    var open = name.IndexOf('[');
                    if (open < 0) { if (name.Length > 0) result.Add(new Segment(name, -1)); break; }
                    var close = name.IndexOf(']', open);
                    if (close < 0) throw new FormatException($"Bad path '{path}'");
                    if (open > 0) result.Add(new Segment(name.Substring(0, open), -1));
                    result.Add(new Segment(null, int.Parse(name.Substring(open + 1, close - open - 1), CultureInfo.InvariantCulture)));
                    name = name.Substring(close + 1);
                }
            }
            return result;
        }

        static FieldInfo Field(Type type, string name) =>
            ConfigFieldWalker.SerializedFields(type).FirstOrDefault(f => f.Name == name)
            ?? throw new MissingFieldException(type.Name, name);

        /// <summary>Type of the value at <paramref name="path"/>, resolved against the current object.</summary>
        public static Type TypeAt(object root, string path)
        {
            object current = root;
            Type type = root.GetType();
            foreach (var s in Parse(path))
            {
                if (s.Index < 0) { var f = Field(type, s.Name); type = f.FieldType; current = current != null ? f.GetValue(current) : null; }
                else { type = ConfigFieldWalker.ElementType(type); current = current is IList l && s.Index < l.Count ? l[s.Index] : null; }
            }
            return type;
        }

        public static object Get(object root, string path)
        {
            object current = root;
            foreach (var s in Parse(path))
            {
                if (current == null) return null;
                current = s.Index < 0 ? Field(current.GetType(), s.Name).GetValue(current) : ((IList)current)[s.Index];
            }
            return current;
        }

        public static void Set(object root, string path, object value)
        {
            var segments = Parse(path);
            SetRecursive(root, segments, 0, value);
        }

        // Returns the (possibly boxed-struct) container after the write so value types propagate upward.
        static object SetRecursive(object container, List<Segment> segments, int i, object value)
        {
            var s = segments[i];
            var last = i == segments.Count - 1;
            if (s.Index < 0)
            {
                var f = Field(container.GetType(), s.Name);
                var next = last ? value : SetRecursive(f.GetValue(container), segments, i + 1, value);
                f.SetValue(container, next);
            }
            else
            {
                var list = (IList)container;
                if (s.Index < 0 || s.Index >= list.Count) throw new IndexOutOfRangeException($"index {s.Index} (count {list.Count})");
                var next = last ? value : SetRecursive(list[s.Index], segments, i + 1, value);
                list[s.Index] = next;
            }
            return container;
        }
    }
}
