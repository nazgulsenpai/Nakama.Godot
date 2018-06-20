// The MIT License (MIT)
//
// Copyright (c) 2018 Alex Parker
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files(the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and / or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions :
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

namespace Nakama.TinyJson
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Runtime.Serialization;
    using System.Text;

    // Really simple JSON parser in ~300 lines
    // - Attempts to parse JSON files with minimal GC allocation
    // - Nice and simple "[1,2,3]".FromJson<List<int>>() API
    // - Classes and structs can be parsed too!
    //      class Foo { public int Value; }
    //      "{\"Value\":10}".FromJson<Foo>()
    // - Can parse JSON without type information into Dictionary<string,object> and List<object> e.g.
    //      "[1,2,3]".FromJson<object>().GetType() == typeof(List<object>)
    //      "{\"Value\":10}".FromJson<object>().GetType() == typeof(Dictionary<string,object>)
    // - No JIT Emit support to support AOT compilation on iOS
    // - Attempts are made to NOT throw an exception if the JSON is corrupted or invalid: returns null instead.
    // - Only public fields and property setters on classes/structs will be written to
    //
    // Limitations:
    // - No JIT Emit support to parse structures quickly
    // - Limited to parsing <2GB JSON files (due to int.MaxValue)
    // - Parsing of abstract classes or interfaces is NOT supported and will throw an exception.
    public static class JsonParser
    {
        [ThreadStatic] private static Stack<List<string>> _splitArrayPool;
        [ThreadStatic] private static StringBuilder _stringBuilder;
        [ThreadStatic] private static Dictionary<Type, Dictionary<string, FieldInfo>> _fieldInfoCache;
        [ThreadStatic] private static Dictionary<Type, Dictionary<string, PropertyInfo>> _propertyInfoCache;

        public static T FromJson<T>(this string json)
        {
            // Initialize, if needed, the ThreadStatic variables
            if (null == _propertyInfoCache)
                _propertyInfoCache = new Dictionary<Type, Dictionary<string, PropertyInfo>>();
            if (null == _fieldInfoCache) _fieldInfoCache = new Dictionary<Type, Dictionary<string, FieldInfo>>();
            if (null == _stringBuilder) _stringBuilder = new StringBuilder();
            if (null == _splitArrayPool) _splitArrayPool = new Stack<List<string>>();

            //Remove all whitespace not within strings to make parsing simpler
            _stringBuilder.Length = 0;
            for (var i = 0; i < json.Length; i++)
            {
                var c = json[i];
                if (c == '"')
                {
                    i = AppendUntilStringEnd(true, i, json);
                    continue;
                }

                if (char.IsWhiteSpace(c))
                    continue;

                _stringBuilder.Append(c);
            }

            //Parse the thing!
            return (T) ParseValue(typeof(T), _stringBuilder.ToString());
        }

        private static int AppendUntilStringEnd(bool appendEscapeCharacter, int startIdx, string json)
        {
            _stringBuilder.Append(json[startIdx]);
            for (var i = startIdx + 1; i < json.Length; i++)
            {
                if (json[i] == '\\')
                {
                    if (appendEscapeCharacter)
                        _stringBuilder.Append(json[i]);
                    _stringBuilder.Append(json[i + 1]);
                    i++; //Skip next character as it is escaped
                }
                else if (json[i] == '"')
                {
                    _stringBuilder.Append(json[i]);
                    return i;
                }
                else
                    _stringBuilder.Append(json[i]);
            }

            return json.Length - 1;
        }

        //Splits { <value>:<value>, <value>:<value> } and [ <value>, <value> ] into a list of <value> strings
        private static List<string> Split(string json)
        {
            var splitArray = _splitArrayPool.Count > 0 ? _splitArrayPool.Pop() : new List<string>();
            splitArray.Clear();
            if (json.Length == 2)
                return splitArray;
            var parseDepth = 0;
            _stringBuilder.Length = 0;
            for (var i = 1; i < json.Length - 1; i++)
            {
                if (json[i] == '[' || json[i] == '{')
                {
                    parseDepth++;
                }
                else if (json[i] == ']' || json[i] == '}')
                {
                    parseDepth--;
                }
                else if (json[i] == '"')
                {
                    i = AppendUntilStringEnd(true, i, json);
                    continue;
                }
                else if (json[i] == ',' || json[i] == ':')
                {
                    if (parseDepth == 0)
                    {
                        splitArray.Add(_stringBuilder.ToString());
                        _stringBuilder.Length = 0;
                        continue;
                    }
                }

                _stringBuilder.Append(json[i]);
            }

            splitArray.Add(_stringBuilder.ToString());

            return splitArray;
        }

        private static object ParseValue(Type type, string json)
        {
            if (type == typeof(string))
            {
                if (json.Length <= 2)
                    return string.Empty;
                var stringBuilder = new StringBuilder();
                for (var i = 1; i < json.Length - 1; ++i)
                {
                    if (json[i] == '\\' && i + 1 < json.Length - 1)
                    {
                        var j = "\"\\nrtbf/".IndexOf(json[i + 1]);
                        if (j >= 0)
                        {
                            stringBuilder.Append("\"\\\n\r\t\b\f/"[j]);
                            ++i;
                            continue;
                        }

                        if (json[i + 1] == 'u' && i + 5 < json.Length - 1)
                        {
                            uint c;
                            if (uint.TryParse(json.Substring(i + 2, 4),
                                System.Globalization.NumberStyles.AllowHexSpecifier, null, out c))
                            {
                                stringBuilder.Append((char) c);
                                i += 5;
                                continue;
                            }
                        }
                    }

                    stringBuilder.Append(json[i]);
                }

                return stringBuilder.ToString();
            }

            if (type == typeof(int))
            {
                int result;
                int.TryParse(json, out result);
                return result;
            }

            if (type == typeof(byte))
            {
                byte result;
                byte.TryParse(json, out result);
                return result;
            }

            if (type == typeof(float))
            {
                float result;
                float.TryParse(json, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out result);
                return result;
            }

            if (type == typeof(double))
            {
                double result;
                double.TryParse(json, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out result);
                return result;
            }

            if (type == typeof(bool))
            {
                return json.ToLower() == "true";
            }

            if (json == "null")
            {
                return null;
            }

            if (type.IsArray)
            {
                var arrayType = type.GetElementType();
                if (json[0] != '[' || json[json.Length - 1] != ']')
                    return null;

                var elems = Split(json);
                var newArray = Array.CreateInstance(arrayType, elems.Count);
                for (var i = 0; i < elems.Count; i++)
                    newArray.SetValue(ParseValue(arrayType, elems[i]), i);
                _splitArrayPool.Push(elems);
                return newArray;
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                var listType = type.GetGenericArguments()[0];
                if (json[0] != '[' || json[json.Length - 1] != ']')
                    return null;

                var elems = Split(json);
                var list = (IList) type.GetConstructor(new Type[] {typeof(int)}).Invoke(new object[] {elems.Count});
                foreach (var t in elems)
                    list.Add(ParseValue(listType, t));

                _splitArrayPool.Push(elems);
                return list;
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                Type keyType, valueType;
                {
                    var args = type.GetGenericArguments();
                    keyType = args[0];
                    valueType = args[1];
                }

                //Refuse to parse dictionary keys that aren't of type string
                if (keyType != typeof(string))
                    return null;
                //Must be a valid dictionary element
                if (json[0] != '{' || json[json.Length - 1] != '}')
                    return null;
                //The list is split into key/value pairs only, this means the split must be divisible by 2 to be valid JSON
                var elems = Split(json);
                if (elems.Count % 2 != 0)
                    return null;

                var dictionary = (IDictionary) type.GetConstructor(new Type[] {typeof(int)})
                    .Invoke(new object[] {elems.Count / 2});
                for (var i = 0; i < elems.Count; i += 2)
                {
                    if (elems[i].Length <= 2)
                        continue;
                    var keyValue = elems[i].Substring(1, elems[i].Length - 2);
                    var val = ParseValue(valueType, elems[i + 1]);
                    dictionary.Add(keyValue, val);
                }

                return dictionary;
            }

            if (type == typeof(object))
            {
                return ParseAnonymousValue(json);
            }

            if (json[0] == '{' && json[json.Length - 1] == '}')
            {
                return ParseObject(type, json);
            }

            return null;
        }

        private static object ParseAnonymousValue(string json)
        {
            if (json.Length == 0)
                return null;
            if (json[0] == '{' && json[json.Length - 1] == '}')
            {
                var elems = Split(json);
                if (elems.Count % 2 != 0)
                    return null;
                var dict = new Dictionary<string, object>(elems.Count / 2);
                for (var i = 0; i < elems.Count; i += 2)
                    dict.Add(elems[i].Substring(1, elems[i].Length - 2), ParseAnonymousValue(elems[i + 1]));
                return dict;
            }

            if (json[0] == '[' && json[json.Length - 1] == ']')
            {
                var items = Split(json);
                var finalList = new List<object>(items.Count);
                foreach (var t in items)
                    finalList.Add(ParseAnonymousValue(t));

                return finalList;
            }

            if (json[0] == '"' && json[json.Length - 1] == '"')
            {
                var str = json.Substring(1, json.Length - 2);
                return str.Replace("\\", string.Empty);
            }

            if (char.IsDigit(json[0]) || json[0] == '-')
            {
                if (json.Contains("."))
                {
                    double result;
                    double.TryParse(json, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out result);
                    return result;
                }
                else
                {
                    int result;
                    int.TryParse(json, out result);
                    return result;
                }
            }

            if (json == "true")
                return true;
            if (json == "false")
                return false;
            // handles json == "null" as well as invalid JSON
            return null;
        }

        private static Dictionary<string, T> CreateMemberNameDictionary<T>(IEnumerable<T> members) where T : MemberInfo
        {
            var nameToMember = new Dictionary<string, T>();
            foreach (var member in members)
            {
                if (member.GetCustomAttribute<IgnoreDataMemberAttribute>() != null)
                    continue;

                string name;
                var dataMemberAttribute = member.GetCustomAttribute<DataMemberAttribute>();
                if (dataMemberAttribute != null && dataMemberAttribute.IsNameSetExplicitly)
                    name = dataMemberAttribute.Name;
                else
                    name = member.Name;

                nameToMember.Add(name, member);
            }

            return nameToMember;
        }

        private static object ParseObject(Type type, string json)
        {
            var instance = FormatterServices.GetUninitializedObject(type);

            //The list is split into key/value pairs only, this means the split must be divisible by 2 to be valid JSON
            var elems = Split(json);
            if (elems.Count % 2 != 0)
                return instance;

            Dictionary<string, FieldInfo> nameToField;
            Dictionary<string, PropertyInfo> nameToProperty;
            if (!_fieldInfoCache.TryGetValue(type, out nameToField))
            {
                var fields =
                    type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
                nameToField = CreateMemberNameDictionary(fields);
                _fieldInfoCache.Add(type, nameToField);
            }

            if (!_propertyInfoCache.TryGetValue(type, out nameToProperty))
            {
                var properties =
                    type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
                nameToProperty = CreateMemberNameDictionary(properties);
                _propertyInfoCache.Add(type, nameToProperty);
            }

            for (var i = 0; i < elems.Count; i += 2)
            {
                if (elems[i].Length <= 2)
                    continue;
                var key = elems[i].Substring(1, elems[i].Length - 2);
                var value = elems[i + 1];

                FieldInfo fieldInfo;
                PropertyInfo propertyInfo;
                if (nameToField.TryGetValue(key, out fieldInfo))
                    fieldInfo.SetValue(instance, ParseValue(fieldInfo.FieldType, value));
                else if (nameToProperty.TryGetValue(key, out propertyInfo))
                    propertyInfo.SetValue(instance, ParseValue(propertyInfo.PropertyType, value), null);
            }

            return instance;
        }
    }
}
