﻿#region License
// Copyright (c) Newtonsoft. All Rights Reserved.
// License: https://raw.github.com/JamesNK/Newtonsoft.Json.Schema/master/LICENSE.md
#endregion

using System;
using System.Collections.Generic;
using System.IO;
#if NET20
using Newtonsoft.Json.Utilities.LinqBridge;
#endif
using Newtonsoft.Json.Schema.Infrastructure;
#if !(NET20 || NET35 || PORTABLE || ASPNETCORE50)
using System.Numerics;
#endif
using System.Text;
#if NETFX_CORE
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
using TestFixture = Microsoft.VisualStudio.TestPlatform.UnitTestFramework.TestClassAttribute;
using Test = Microsoft.VisualStudio.TestPlatform.UnitTestFramework.TestMethodAttribute;
#elif ASPNETCORE50
using Xunit;
using Test = Xunit.FactAttribute;
using Assert = Newtonsoft.Json.Tests.XUnitAssert;
#else
using NUnit.Framework;
#endif
using System.Xml;
using System.Xml.Schema;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using Newtonsoft.Json.Schema.V4;
using Newtonsoft.Json.Utilities;

namespace Newtonsoft.Json.Schema.Tests
{
    [TestFixture]
    public class JValidatingReaderTests : TestFixtureBase
    {
        private JSchema4ValidatingReader CreateReader(string json, JSchema4 schema, out IList<SchemaValidationEventArgs> errors)
        {
            JsonReader reader = new JsonTextReader(new StringReader(json));

            List<SchemaValidationEventArgs> localErrors = new List<SchemaValidationEventArgs>();

            JSchema4ValidatingReader validatingReader = new JSchema4ValidatingReader(reader);
            validatingReader.ValidationEventHandler += (sender, args) => { localErrors.Add(args); };
            validatingReader.Schema = schema;

            errors = localErrors;
            return validatingReader;
        }

        [Test]
        public void ValidateInteger()
        {
            JSchema4 schema = new JSchema4();
            schema.Type = JSchemaType.Integer;

            string json = "1";

            IList<SchemaValidationEventArgs> errors;
            JSchema4ValidatingReader validatingReader = CreateReader(json, schema, out errors);

            Assert.IsTrue(validatingReader.Read());

            Assert.AreEqual(0, errors.Count);
        }

        [Test]
        public void ValidateObject()
        {
            JSchema4 schema = new JSchema4();
            schema.Type = JSchemaType.Object;

            string json = "{}";

            IList<SchemaValidationEventArgs> errors;
            JSchema4ValidatingReader validatingReader = CreateReader(json, schema, out errors);

            Assert.IsTrue(validatingReader.Read());
            Assert.IsTrue(validatingReader.Read());
            Assert.IsFalse(validatingReader.Read());

            Assert.AreEqual(0, errors.Count);
        }

        [Test]
        public void ValidateObjectWithProperty()
        {
            JSchema4 schema = new JSchema4();
            schema.Type = JSchemaType.Object;
            schema.Properties = new Dictionary<string, JSchema4>
            {
                { "testProp", new JSchema4 { Type = JSchemaType.Boolean } },
                { "testProp2", new JSchema4 { Type = JSchemaType.Integer } }
            };

            string json = "{'testProp':5,'testProp2':true}";

            IList<SchemaValidationEventArgs> errors;
            JSchema4ValidatingReader validatingReader = CreateReader(json, schema, out errors);

            Assert.IsTrue(validatingReader.Read());
            Assert.IsTrue(validatingReader.Read());
            Assert.IsTrue(validatingReader.Read());
            Assert.IsTrue(validatingReader.Read());
            Assert.IsTrue(validatingReader.Read());
            Assert.IsTrue(validatingReader.Read());
            Assert.IsFalse(validatingReader.Read());

            Assert.AreEqual(2, errors.Count);
            Assert.AreEqual("Invalid type. Expected Boolean but got Integer. Line 1, position 13.", errors[0].Message);
            Assert.AreEqual("Invalid type. Expected Integer but got Boolean. Line 1, position 30.", errors[1].Message);
        }

        [Test]
        public void ValidateArray()
        {
            JSchema4 schema = new JSchema4();
            schema.Type = JSchemaType.Array;
            schema.Items.Add(new JSchema4 { Type = JSchemaType.Integer });

            string json = "[1,true,2,'hi']";

            IList<SchemaValidationEventArgs> errors;
            JSchema4ValidatingReader validatingReader = CreateReader(json, schema, out errors);

            Assert.IsTrue(validatingReader.Read());
            Assert.IsTrue(validatingReader.Read());
            Assert.IsTrue(validatingReader.Read());
            Assert.IsTrue(validatingReader.Read());
            Assert.IsTrue(validatingReader.Read());
            Assert.IsTrue(validatingReader.Read());
            Assert.IsFalse(validatingReader.Read());

            Assert.AreEqual(2, errors.Count);
            StringAssert.AreEqual(@"Invalid type. Expected Integer but got Boolean. Line 1, position 7.", errors[0].Message);
            StringAssert.AreEqual(@"Invalid type. Expected Integer but got String. Line 1, position 14.", errors[1].Message);
        }

        [Test]
        public void ValidateInteger_AllOfFailure()
        {
            JSchema4 schema = new JSchema4();
            schema.Type = JSchemaType.Integer;

            schema.AllOf.Add(new JSchema4
            {
                Maximum = 10
            });
            schema.AllOf.Add(new JSchema4
            {
                Minimum = 2
            });

            string json = "1";

            IList<SchemaValidationEventArgs> errors;
            JSchema4ValidatingReader validatingReader = CreateReader(json, schema, out errors);

            Assert.IsTrue(validatingReader.Read());

            Assert.AreEqual(1, errors.Count);
        }

        [Test]
        public void ValidateInteger_BadTypeFailure()
        {
            JSchema4 schema = new JSchema4();
            schema.Type = JSchemaType.Boolean;

            string json = "1";
            JsonReader reader = new JsonTextReader(new StringReader(json));

            SchemaValidationEventArgs validationEventArgs = null;
            JSchema4ValidatingReader validatingReader = new JSchema4ValidatingReader(reader);
            validatingReader.ValidationEventHandler += (sender, args) => { validationEventArgs = args; };
            validatingReader.Schema = schema;

            Assert.IsTrue(validatingReader.Read());

            Assert.IsNotNull(validationEventArgs);
            StringAssert.AreEqual(@"Invalid type. Expected Boolean but got Integer. Line 1, position 1.", validationEventArgs.Message);
        }

        [Test]
        public void CheckInnerReader()
        {
            string json = "{'name':'James','hobbies':['pie','cake']}";
            JsonReader reader = new JsonTextReader(new StringReader(json));

            JSchema4ValidatingReader validatingReader = new JSchema4ValidatingReader(reader);
            Assert.AreEqual(reader, validatingReader.Reader);
        }

        [Test]
        public void ValidateTypes()
        {
            string schemaJson = @"{
  ""description"":""A person"",
  ""type"":""object"",
  ""properties"":
  {
    ""name"":{""type"":""string""},
    ""hobbies"":
    {
      ""type"":""array"",
      ""items"": {""type"":""string""}
    }
  }
}";

            string json = @"{'name':""James"",'hobbies':[""pie"",'cake']}";

            SchemaValidationEventArgs validationEventArgs = null;

            JSchema4ValidatingReader reader = new JSchema4ValidatingReader(new JsonTextReader(new StringReader(json)));
            reader.ValidationEventHandler += (sender, args) => { validationEventArgs = args; };
            JSchema4 schema = JSchema4.Parse(schemaJson);
            reader.Schema = schema;
            Assert.AreEqual(schema, reader.Schema);
            Assert.AreEqual(0, reader.Depth);
            Assert.AreEqual("", reader.Path);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.StartObject, reader.TokenType);
            Assert.AreEqual("", reader.Path);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.PropertyName, reader.TokenType);
            Assert.AreEqual("name", reader.Value.ToString());
            Assert.AreEqual("name", reader.Path);
            Assert.AreEqual(1, reader.Depth);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.String, reader.TokenType);
            Assert.AreEqual("James", reader.Value.ToString());
            Assert.AreEqual(typeof(string), reader.ValueType);
            Assert.AreEqual('"', reader.QuoteChar);
            Assert.AreEqual("name", reader.Path);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.PropertyName, reader.TokenType);
            Assert.AreEqual("hobbies", reader.Value.ToString());
            Assert.AreEqual('\'', reader.QuoteChar);
            Assert.AreEqual("hobbies", reader.Path);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.StartArray, reader.TokenType);
            Assert.AreEqual("hobbies", reader.Path);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.String, reader.TokenType);
            Assert.AreEqual("pie", reader.Value.ToString());
            Assert.AreEqual('"', reader.QuoteChar);
            Assert.AreEqual("hobbies[0]", reader.Path);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.String, reader.TokenType);
            Assert.AreEqual("cake", reader.Value.ToString());
            Assert.AreEqual("hobbies[1]", reader.Path);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.EndArray, reader.TokenType);
            Assert.AreEqual("hobbies", reader.Path);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.EndObject, reader.TokenType);
            Assert.AreEqual("", reader.Path);

            Assert.IsFalse(reader.Read());

            Assert.IsNull(validationEventArgs);
        }

        [Test]
        public void ValidateUnrestrictedArray()
        {
            string schemaJson = @"{
  ""type"":""array""
}";

            string json = "['pie','cake',['nested1','nested2'],{'nestedproperty1':1.1,'nestedproperty2':[null]}]";

            Json.Schema.SchemaValidationEventArgs validationEventArgs = null;

            JSchema4ValidatingReader reader = new JSchema4ValidatingReader(new JsonTextReader(new StringReader(json)));
            reader.ValidationEventHandler += (sender, args) => { validationEventArgs = args; };
            reader.Schema = JSchema4.Parse(schemaJson);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.StartArray, reader.TokenType);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.String, reader.TokenType);
            Assert.AreEqual("pie", reader.Value.ToString());

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.String, reader.TokenType);
            Assert.AreEqual("cake", reader.Value.ToString());

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.StartArray, reader.TokenType);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.String, reader.TokenType);
            Assert.AreEqual("nested1", reader.Value.ToString());

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.String, reader.TokenType);
            Assert.AreEqual("nested2", reader.Value.ToString());

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.EndArray, reader.TokenType);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.StartObject, reader.TokenType);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.PropertyName, reader.TokenType);
            Assert.AreEqual("nestedproperty1", reader.Value.ToString());

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Float, reader.TokenType);
            Assert.AreEqual(1.1, reader.Value);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.PropertyName, reader.TokenType);
            Assert.AreEqual("nestedproperty2", reader.Value.ToString());

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.StartArray, reader.TokenType);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Null, reader.TokenType);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.EndArray, reader.TokenType);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.EndObject, reader.TokenType);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.EndArray, reader.TokenType);

            Assert.IsNull(validationEventArgs);
        }

        [Test]
        public void StringLessThanMinimumLength()
        {
            string schemaJson = @"{
  ""type"":""string"",
  ""minLength"":5,
  ""maxLength"":50,
}";

            string json = "'pie'";

            SchemaValidationEventArgs validationEventArgs = null;

            JSchema4ValidatingReader reader = new JSchema4ValidatingReader(new JsonTextReader(new StringReader(json)));
            reader.ValidationEventHandler += (sender, args) => { validationEventArgs = args; };
            reader.Schema = JSchema4.Parse(schemaJson);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.String, reader.TokenType);
            Assert.AreEqual("String 'pie' is less than minimum length of 5. Line 1, position 5.", validationEventArgs.Message);

            Assert.IsNotNull(validationEventArgs);
        }

        [Test]
        public void StringGreaterThanMaximumLength()
        {
            string schemaJson = @"{
  ""type"":""string"",
  ""minLength"":5,
  ""maxLength"":10
}";

            string json = "'The quick brown fox jumps over the lazy dog.'";

            SchemaValidationEventArgs validationEventArgs = null;

            JSchema4ValidatingReader reader = new JSchema4ValidatingReader(new JsonTextReader(new StringReader(json)));
            reader.ValidationEventHandler += (sender, args) => { validationEventArgs = args; };
            reader.Schema = JSchema4.Parse(schemaJson);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.String, reader.TokenType);
            Assert.AreEqual("String 'The quick brown fox jumps over the lazy dog.' exceeds maximum length of 10. Line 1, position 46.", validationEventArgs.Message);

            Assert.IsNotNull(validationEventArgs);
        }

        [Test]
        public void StringIsNotInEnum()
        {
            string schemaJson = @"{
  ""type"":""array"",
  ""items"":{
    ""type"":""string"",
    ""enum"":[""one"",""two""]
  },
  ""maxItems"":3
}";

            string json = "['one','two','THREE']";

            Json.Schema.SchemaValidationEventArgs validationEventArgs = null;

            JSchema4ValidatingReader reader = new JSchema4ValidatingReader(new JsonTextReader(new StringReader(json)));
            reader.ValidationEventHandler += (sender, args) => { validationEventArgs = args; };
            reader.Schema = JSchema4.Parse(schemaJson);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.StartArray, reader.TokenType);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.String, reader.TokenType);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.String, reader.TokenType);
            Assert.AreEqual(null, validationEventArgs);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.String, reader.TokenType);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.EndArray, reader.TokenType);

            Assert.IsNotNull(validationEventArgs);
            Assert.AreEqual(@"Value ""THREE"" is not defined in enum. Line 1, position 20.", validationEventArgs.Message);
            Assert.AreEqual("[2]", validationEventArgs.Path);
        }

        [Test]
        public void StringDoesNotMatchPattern()
        {
            string schemaJson = @"{
  ""type"":""string"",
  ""pattern"":""foo""
}";

            string json = "'The quick brown fox jumps over the lazy dog.'";

            SchemaValidationEventArgs validationEventArgs = null;

            JSchema4ValidatingReader reader = new JSchema4ValidatingReader(new JsonTextReader(new StringReader(json)));
            reader.ValidationEventHandler += (sender, args) => { validationEventArgs = args; };
            reader.Schema = JSchema4.Parse(schemaJson);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.String, reader.TokenType);
            Assert.AreEqual("String 'The quick brown fox jumps over the lazy dog.' does not match regex pattern 'foo'. Line 1, position 46.", validationEventArgs.Message);
            Assert.AreEqual("", validationEventArgs.Path);

            Assert.IsNotNull(validationEventArgs);
        }

        [Test]
        public void IntegerGreaterThanMaximumValue()
        {
            string schemaJson = @"{
  ""type"":""integer"",
  ""maximum"":5
}";

            string json = "10";

            Json.Schema.SchemaValidationEventArgs validationEventArgs = null;

            JSchema4ValidatingReader reader = new JSchema4ValidatingReader(new JsonTextReader(new StringReader(json)));
            reader.ValidationEventHandler += (sender, args) => { validationEventArgs = args; };
            reader.Schema = JSchema4.Parse(schemaJson);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Integer, reader.TokenType);
            Assert.AreEqual("Integer 10 exceeds maximum value of 5. Line 1, position 2.", validationEventArgs.Message);
            Assert.AreEqual("", validationEventArgs.Path);

            Assert.IsNotNull(validationEventArgs);
        }

#if !(NET20 || NET35 || PORTABLE || ASPNETCORE50 || PORTABLE40)
        [Test]
        public void IntegerGreaterThanMaximumValue_BigInteger()
        {
            string schemaJson = @"{
  ""type"":""integer"",
  ""maximum"":5
}";

            string json = "99999999999999999999999999999999999999999999999999999999999999999999";

            Json.Schema.ValidationEventArgs validationEventArgs = null;

            JsonValidatingReader reader = new JsonValidatingReader(new JsonTextReader(new StringReader(json)));
            reader.ValidationEventHandler += (sender, args) => { validationEventArgs = args; };
            reader.Schema = JsonSchema.Parse(schemaJson);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Integer, reader.TokenType);
            Assert.AreEqual("Integer 99999999999999999999999999999999999999999999999999999999999999999999 exceeds maximum value of 5. Line 1, position 68.", validationEventArgs.Message);
            Assert.AreEqual("", validationEventArgs.Path);

            Assert.IsNotNull(validationEventArgs);
        }

        [Test]
        public void IntegerLessThanMaximumValue_BigInteger()
        {
            string schemaJson = @"{
  ""type"":""integer"",
  ""minimum"":5
}";

            JValue v = new JValue(new BigInteger(1));

            Json.Schema.ValidationEventArgs validationEventArgs = null;

            v.Validate(JsonSchema.Parse(schemaJson), (sender, args) => { validationEventArgs = args; });

            Assert.IsNotNull(validationEventArgs);
            Assert.AreEqual("Integer 1 is less than minimum value of 5.", validationEventArgs.Message);
            Assert.AreEqual("", validationEventArgs.Path);
        }
#endif

        [Test]
        public void ThrowExceptionWhenNoValidationEventHandler()
        {
            ExceptionAssert.Throws<JSchemaException>(() =>
            {
                string schemaJson = @"{
  ""type"":""integer"",
  ""maximum"":5
}";

                JSchema4ValidatingReader reader = new JSchema4ValidatingReader(new JsonTextReader(new StringReader("10")));
                reader.Schema = JSchema4.Parse(schemaJson);

                Assert.IsTrue(reader.Read());
            }, "Integer 10 exceeds maximum value of 5. Line 1, position 2.");
        }

        [Test]
        public void IntegerLessThanMinimumValue()
        {
            string schemaJson = @"{
  ""type"":""integer"",
  ""minimum"":5
}";

            string json = "1";

            Json.Schema.SchemaValidationEventArgs validationEventArgs = null;

            JSchema4ValidatingReader reader = new JSchema4ValidatingReader(new JsonTextReader(new StringReader(json)));
            reader.ValidationEventHandler += (sender, args) => { validationEventArgs = args; };
            reader.Schema = JSchema4.Parse(schemaJson);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Integer, reader.TokenType);
            Assert.AreEqual("Integer 1 is less than minimum value of 5. Line 1, position 1.", validationEventArgs.Message);

            Assert.IsNotNull(validationEventArgs);
        }

        [Test]
        public void IntegerIsNotInEnum()
        {
            string schemaJson = @"{
  ""type"":""array"",
  ""items"":{
    ""type"":""integer"",
    ""enum"":[1,2]
  },
  ""maxItems"":3
}";

            string json = "[1,2,3]";

            Json.Schema.SchemaValidationEventArgs validationEventArgs = null;

            JSchema4ValidatingReader reader = new JSchema4ValidatingReader(new JsonTextReader(new StringReader(json)));
            reader.ValidationEventHandler += (sender, args) => { validationEventArgs = args; };
            reader.Schema = JSchema4.Parse(schemaJson);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.StartArray, reader.TokenType);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Integer, reader.TokenType);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Integer, reader.TokenType);
            Assert.AreEqual(null, validationEventArgs);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Integer, reader.TokenType);
            Assert.AreEqual(@"Value 3 is not defined in enum. Line 1, position 6.", validationEventArgs.Message);
            Assert.AreEqual("[2]", validationEventArgs.Path);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.EndArray, reader.TokenType);

            Assert.IsNotNull(validationEventArgs);
        }

        [Test]
        public void FloatGreaterThanMaximumValue()
        {
            string schemaJson = @"{
  ""type"":""number"",
  ""maximum"":5
}";

            string json = "10.0";

            Json.Schema.SchemaValidationEventArgs validationEventArgs = null;

            JSchema4ValidatingReader reader = new JSchema4ValidatingReader(new JsonTextReader(new StringReader(json)));
            reader.ValidationEventHandler += (sender, args) => { validationEventArgs = args; };
            reader.Schema = JSchema4.Parse(schemaJson);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Float, reader.TokenType);
            Assert.AreEqual("Float 10.0 exceeds maximum value of 5. Line 1, position 4.", validationEventArgs.Message);

            Assert.IsNotNull(validationEventArgs);
        }

        [Test]
        public void FloatLessThanMinimumValue()
        {
            string schemaJson = @"{
  ""type"":""number"",
  ""minimum"":5
}";

            string json = "1.1";

            Json.Schema.SchemaValidationEventArgs validationEventArgs = null;

            JSchema4ValidatingReader reader = new JSchema4ValidatingReader(new JsonTextReader(new StringReader(json)));
            reader.ValidationEventHandler += (sender, args) => { validationEventArgs = args; };
            reader.Schema = JSchema4.Parse(schemaJson);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Float, reader.TokenType);
            Assert.AreEqual("Float 1.1 is less than minimum value of 5. Line 1, position 3.", validationEventArgs.Message);

            Assert.IsNotNull(validationEventArgs);
        }

        [Test]
        public void FloatIsNotInEnum()
        {
            string schemaJson = @"{
  ""type"":""array"",
  ""items"":{
    ""type"":""number"",
    ""enum"":[1.1,2.2]
  },
  ""maxItems"":3
}";

            string json = "[1.1,2.2,3.0]";

            Json.Schema.SchemaValidationEventArgs validationEventArgs = null;

            JSchema4ValidatingReader reader = new JSchema4ValidatingReader(new JsonTextReader(new StringReader(json)));
            reader.ValidationEventHandler += (sender, args) => { validationEventArgs = args; };
            reader.Schema = JSchema4.Parse(schemaJson);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.StartArray, reader.TokenType);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Float, reader.TokenType);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Float, reader.TokenType);
            Assert.AreEqual(null, validationEventArgs);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Float, reader.TokenType);
            Assert.AreEqual(@"Value 3.0 is not defined in enum. Line 1, position 12.", validationEventArgs.Message);
            Assert.AreEqual("[2]", validationEventArgs.Path);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.EndArray, reader.TokenType);

            Assert.IsNotNull(validationEventArgs);
        }

        [Test]
        public void FloatDivisibleBy()
        {
            string schemaJson = @"{
  ""type"":""array"",
  ""items"":{
    ""type"":""number"",
    ""divisibleBy"":0.1
  }
}";

            string json = "[1.1,2.2,4.001]";

            Json.Schema.SchemaValidationEventArgs validationEventArgs = null;

            JSchema4ValidatingReader reader = new JSchema4ValidatingReader(new JsonTextReader(new StringReader(json)));
            reader.ValidationEventHandler += (sender, args) => { validationEventArgs = args; };
            reader.Schema = JSchema4.Parse(schemaJson);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.StartArray, reader.TokenType);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Float, reader.TokenType);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Float, reader.TokenType);
            Assert.AreEqual(null, validationEventArgs);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Float, reader.TokenType);
            Assert.AreEqual(@"Float 4.001 is not a multiple of 0.1. Line 1, position 14.", validationEventArgs.Message);
            Assert.AreEqual("[2]", validationEventArgs.Path);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.EndArray, reader.TokenType);

            Assert.IsNotNull(validationEventArgs);
        }

#if !(NET20 || NET35 || PORTABLE || ASPNETCORE50 || PORTABLE40)
        [Test]
        public void BigIntegerDivisibleBy_Success()
        {
            string schemaJson = @"{
  ""type"":""array"",
  ""items"":{
    ""type"":""number"",
    ""divisibleBy"":2
  }
}";

            string json = "[999999999999999999999999999999999999999999999999999999998]";

            Json.Schema.ValidationEventArgs validationEventArgs = null;

            JsonValidatingReader reader = new JsonValidatingReader(new JsonTextReader(new StringReader(json)));
            reader.ValidationEventHandler += (sender, args) => { validationEventArgs = args; };
            reader.Schema = JsonSchema.Parse(schemaJson);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.StartArray, reader.TokenType);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Integer, reader.TokenType);
            Assert.IsNull(validationEventArgs);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.EndArray, reader.TokenType);
        }

        [Test]
        public void BigIntegerDivisibleBy_Failure()
        {
            string schemaJson = @"{
  ""type"":""array"",
  ""items"":{
    ""type"":""number"",
    ""divisibleBy"":2
  }
}";

            string json = "[999999999999999999999999999999999999999999999999999999999]";

            Json.Schema.ValidationEventArgs validationEventArgs = null;

            JsonValidatingReader reader = new JsonValidatingReader(new JsonTextReader(new StringReader(json)));
            reader.ValidationEventHandler += (sender, args) => { validationEventArgs = args; };
            reader.Schema = JsonSchema.Parse(schemaJson);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.StartArray, reader.TokenType);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Integer, reader.TokenType);
            Assert.AreEqual(@"Integer 999999999999999999999999999999999999999999999999999999999 is not evenly divisible by 2. Line 1, position 58.", validationEventArgs.Message);
            Assert.AreEqual("[0]", validationEventArgs.Path);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.EndArray, reader.TokenType);

            Assert.IsNotNull(validationEventArgs);
        }

        [Test]
        public void BigIntegerDivisibleBy_Fraction()
        {
            string schemaJson = @"{
  ""type"":""array"",
  ""items"":{
    ""type"":""number"",
    ""divisibleBy"":1.1
  }
}";

            string json = "[999999999999999999999999999999999999999999999999999999999]";

            Json.Schema.ValidationEventArgs validationEventArgs = null;

            JsonValidatingReader reader = new JsonValidatingReader(new JsonTextReader(new StringReader(json)));
            reader.ValidationEventHandler += (sender, args) => { validationEventArgs = args; };
            reader.Schema = JsonSchema.Parse(schemaJson);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.StartArray, reader.TokenType);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Integer, reader.TokenType);
            Assert.IsNotNull(validationEventArgs);
            Assert.AreEqual(@"Integer 999999999999999999999999999999999999999999999999999999999 is not evenly divisible by 1.1. Line 1, position 58.", validationEventArgs.Message);
            Assert.AreEqual("[0]", validationEventArgs.Path);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.EndArray, reader.TokenType);
        }

        [Test]
        public void BigIntegerDivisibleBy_FractionWithZeroValue()
        {
            string schemaJson = @"{
  ""type"":""array"",
  ""items"":{
    ""type"":""number"",
    ""divisibleBy"":1.1
  }
}";

            JArray a = new JArray(new JValue(new BigInteger(0)));

            ValidationEventArgs validationEventArgs = null;

            a.Validate(JsonSchema.Parse(schemaJson), (sender, args) => { validationEventArgs = args; });

            Assert.IsNull(validationEventArgs);
        }
#endif

        [Test]
        public void IntValidForNumber()
        {
            string schemaJson = @"{
  ""type"":""array"",
  ""items"":{
    ""type"":""number""
  }
}";

            string json = "[1]";

            Json.Schema.SchemaValidationEventArgs validationEventArgs = null;

            JSchema4ValidatingReader reader = new JSchema4ValidatingReader(new JsonTextReader(new StringReader(json)));
            reader.ValidationEventHandler += (sender, args) => { validationEventArgs = args; };
            reader.Schema = JSchema4.Parse(schemaJson);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.StartArray, reader.TokenType);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Integer, reader.TokenType);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.EndArray, reader.TokenType);

            Assert.IsNull(validationEventArgs);
        }

        [Test]
        public void NullNotInEnum()
        {
            string schemaJson = @"{
  ""type"":""array"",
  ""items"":{
    ""type"":""null"",
    ""enum"":[]
  },
  ""maxItems"":3
}";

            string json = "[null]";

            Json.Schema.SchemaValidationEventArgs validationEventArgs = null;

            JSchema4ValidatingReader reader = new JSchema4ValidatingReader(new JsonTextReader(new StringReader(json)));
            reader.ValidationEventHandler += (sender, args) => { validationEventArgs = args; };
            reader.Schema = JSchema4.Parse(schemaJson);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.StartArray, reader.TokenType);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Null, reader.TokenType);
            Assert.AreEqual(@"Value null is not defined in enum. Line 1, position 5.", validationEventArgs.Message);
            Assert.AreEqual("[0]", validationEventArgs.Path);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.EndArray, reader.TokenType);

            Assert.IsNotNull(validationEventArgs);
        }

        [Test]
        public void BooleanNotInEnum()
        {
            string schemaJson = @"{
  ""type"":""array"",
  ""items"":{
    ""type"":""boolean"",
    ""enum"":[true]
  },
  ""maxItems"":3
}";

            string json = "[true,false]";

            Json.Schema.SchemaValidationEventArgs validationEventArgs = null;

            JSchema4ValidatingReader reader = new JSchema4ValidatingReader(new JsonTextReader(new StringReader(json)));
            reader.ValidationEventHandler += (sender, args) => { validationEventArgs = args; };
            reader.Schema = JSchema4.Parse(schemaJson);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.StartArray, reader.TokenType);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Boolean, reader.TokenType);
            Assert.AreEqual(null, validationEventArgs);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Boolean, reader.TokenType);
            Assert.AreEqual(@"Value false is not defined in enum. Line 1, position 11.", validationEventArgs.Message);
            Assert.AreEqual("[1]", validationEventArgs.Path);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.EndArray, reader.TokenType);

            Assert.IsNotNull(validationEventArgs);
        }

        [Test]
        public void ArrayCountGreaterThanMaximumItems()
        {
            string schemaJson = @"{
  ""type"":""array"",
  ""minItems"":2,
  ""maxItems"":3
}";

            string json = "[null,null,null,null]";

            Json.Schema.SchemaValidationEventArgs validationEventArgs = null;

            JSchema4ValidatingReader reader = new JSchema4ValidatingReader(new JsonTextReader(new StringReader(json)));
            reader.ValidationEventHandler += (sender, args) => { validationEventArgs = args; };
            reader.Schema = JSchema4.Parse(schemaJson);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.StartArray, reader.TokenType);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Null, reader.TokenType);
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Null, reader.TokenType);
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Null, reader.TokenType);
            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Null, reader.TokenType);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.EndArray, reader.TokenType);

            Assert.IsNotNull(validationEventArgs);
            Assert.AreEqual("Array item count 4 exceeds maximum count of 3. Line 1, position 21.", validationEventArgs.Message);
        }

        [Test]
        public void ArrayCountLessThanMinimumItems()
        {
            string schemaJson = @"{
  ""type"":""array"",
  ""minItems"":2,
  ""maxItems"":3
}";

            string json = "[null]";

            SchemaValidationEventArgs validationEventArgs = null;

            JSchema4ValidatingReader reader = new JSchema4ValidatingReader(new JsonTextReader(new StringReader(json)));
            reader.ValidationEventHandler += (sender, args) => { validationEventArgs = args; };
            reader.Schema = JSchema4.Parse(schemaJson);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.StartArray, reader.TokenType);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Null, reader.TokenType);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.EndArray, reader.TokenType);
            Assert.AreEqual("Array item count 1 is less than minimum count of 2. Line 1, position 6.", validationEventArgs.Message);

            Assert.IsNotNull(validationEventArgs);
        }

        [Test]
        public void InvalidDataType()
        {
            string schemaJson = @"{
  ""type"":""string"",
  ""minItems"":2,
  ""maxItems"":3,
  ""items"":{}
}";

            string json = "[null,null,null,null]";

            SchemaValidationEventArgs validationEventArgs = null;

            JSchema4ValidatingReader reader = new JSchema4ValidatingReader(new JsonTextReader(new StringReader(json)));
            reader.ValidationEventHandler += (sender, args) => { validationEventArgs = args; };
            reader.Schema = JSchema4.Parse(schemaJson);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.StartArray, reader.TokenType);
            Assert.AreEqual(@"Invalid type. Expected String but got Array. Line 1, position 1.", validationEventArgs.Message);

            Assert.IsNotNull(validationEventArgs);
        }

        [Test]
        public void StringDisallowed()
        {
            string schemaJson = @"{
  ""type"":""array"",
  ""items"":{
    ""disallow"":[""number""]
  },
  ""maxItems"":3
}";

            string json = "['pie',1.1]";

            SchemaValidationEventArgs validationEventArgs = null;

            JSchema4ValidatingReader reader = new JSchema4ValidatingReader(new JsonTextReader(new StringReader(json)));
            reader.ValidationEventHandler += (sender, args) => { validationEventArgs = args; };
            reader.Schema = JSchema4.Parse(schemaJson);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.StartArray, reader.TokenType);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.String, reader.TokenType);
            Assert.AreEqual(null, validationEventArgs);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Float, reader.TokenType);

            Assert.IsNotNull(validationEventArgs);
            Assert.AreEqual(@"Not Line 1, position 10.", validationEventArgs.Message);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.EndArray, reader.TokenType);

        }

        [Test]
        public void MissingRequiredProperties()
        {
            string schemaJson = @"{
  ""description"":""A person"",
  ""type"":""object"",
  ""properties"":
  {
    ""name"":{""type"":""string""},
    ""hobbies"":{""type"":""string"",""required"":true},
    ""age"":{""type"":""integer"",""required"":true}
  }
}";

            string json = "{'name':'James'}";

            Json.Schema.SchemaValidationEventArgs validationEventArgs = null;

            JSchema4ValidatingReader reader = new JSchema4ValidatingReader(new JsonTextReader(new StringReader(json)));
            reader.ValidationEventHandler += (sender, args) => { validationEventArgs = args; };
            reader.Schema = JSchema4.Parse(schemaJson);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.StartObject, reader.TokenType);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.PropertyName, reader.TokenType);
            Assert.AreEqual("name", reader.Value.ToString());

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.String, reader.TokenType);
            Assert.AreEqual("James", reader.Value.ToString());
            Assert.AreEqual(null, validationEventArgs);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.EndObject, reader.TokenType);
            Assert.AreEqual("Required properties are missing from object: hobbies, age. Line 1, position 16.", validationEventArgs.Message);
            Assert.AreEqual("", validationEventArgs.Path);

            Assert.IsNotNull(validationEventArgs);
        }

        [Test]
        public void MissingNonRequiredProperties()
        {
            string schemaJson = @"{
  ""description"":""A person"",
  ""type"":""object"",
  ""properties"":
  {
    ""name"":{""type"":""string"",""required"":true},
    ""hobbies"":{""type"":""string"",""required"":false},
    ""age"":{""type"":""integer""}
  }
}";

            string json = "{'name':'James'}";

            Json.Schema.SchemaValidationEventArgs validationEventArgs = null;

            JSchema4ValidatingReader reader = new JSchema4ValidatingReader(new JsonTextReader(new StringReader(json)));
            reader.ValidationEventHandler += (sender, args) => { validationEventArgs = args; };
            reader.Schema = JSchema4.Parse(schemaJson);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.StartObject, reader.TokenType);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.PropertyName, reader.TokenType);
            Assert.AreEqual("name", reader.Value.ToString());

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.String, reader.TokenType);
            Assert.AreEqual("James", reader.Value.ToString());
            Assert.IsNull(validationEventArgs);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.EndObject, reader.TokenType);

            Assert.IsNull(validationEventArgs);
        }

        [Test]
        public void DisableAdditionalProperties()
        {
            string schemaJson = @"{
  ""description"":""A person"",
  ""type"":""object"",
  ""properties"":
  {
    ""name"":{""type"":""string""}
  },
  ""additionalProperties"":false
}";

            string json = "{'name':'James','additionalProperty1':null,'additionalProperty2':null}";

            Json.Schema.SchemaValidationEventArgs validationEventArgs = null;

            JSchema4ValidatingReader reader = new JSchema4ValidatingReader(new JsonTextReader(new StringReader(json)));
            reader.ValidationEventHandler += (sender, args) => { validationEventArgs = args; };
            reader.Schema = JSchema4.Parse(schemaJson);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.StartObject, reader.TokenType);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.PropertyName, reader.TokenType);
            Assert.AreEqual("name", reader.Value.ToString());

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.String, reader.TokenType);
            Assert.AreEqual("James", reader.Value.ToString());
            Assert.AreEqual(null, validationEventArgs);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.PropertyName, reader.TokenType);
            Assert.AreEqual("additionalProperty1", reader.Value.ToString());

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Null, reader.TokenType);
            Assert.AreEqual(null, reader.Value);

            Assert.IsNotNull(validationEventArgs);
            Assert.AreEqual("Property 'additionalProperty1' has not been defined and the schema does not allow additional properties. Line 1, position 38.", validationEventArgs.Message);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.PropertyName, reader.TokenType);
            Assert.AreEqual("additionalProperty2", reader.Value.ToString());

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Null, reader.TokenType);
            Assert.AreEqual(null, reader.Value);
            Assert.AreEqual("Property 'additionalProperty2' has not been defined and the schema does not allow additional properties. Line 1, position 65.", validationEventArgs.Message);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.EndObject, reader.TokenType);
        }

        [Test]
        public void ExtendsStringGreaterThanMaximumLength()
        {
            string schemaJson = @"{
  ""extends"":{
    ""type"":""string"",
    ""minLength"":5,
    ""maxLength"":10
  },
  ""maxLength"":9
}";

            List<string> errors = new List<string>();
            string json = "'The quick brown fox jumps over the lazy dog.'";

            Json.Schema.SchemaValidationEventArgs validationEventArgs = null;

            JSchema4ValidatingReader reader = new JSchema4ValidatingReader(new JsonTextReader(new StringReader(json)));
            reader.ValidationEventHandler += (sender, args) =>
            {
                validationEventArgs = args;
                errors.Add(validationEventArgs.Message);
            };
            reader.Schema = JSchema4.Parse(schemaJson);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.String, reader.TokenType);
            Assert.AreEqual(2, errors.Count);
            Assert.AreEqual("AllOf Line 1, position 46.", errors[0]);
            Assert.AreEqual("String 'The quick brown fox jumps over the lazy dog.' exceeds maximum length of 9. Line 1, position 46.", errors[1]);

            Assert.IsNotNull(validationEventArgs);
        }

        private JSchema4 GetExtendedSchema()
        {
            string first = @"{
  ""id"":""first"",
  ""type"":""object"",
  ""properties"":
  {
    ""firstproperty"":{""type"":""string"",""required"":true}
  },
  ""additionalProperties"":{}
}";

            string second = @"{
  ""id"":""second"",
  ""type"":""object"",
  ""extends"":{""$ref"":""first""},
  ""properties"":
  {
    ""secondproperty"":{""type"":""string"",""required"":true}
  },
  ""additionalProperties"":false
}";

            JSchemaPreloadedResolver resolver = new JSchemaPreloadedResolver();
            JSchema4 firstSchema = JSchema4.Parse(first);

            resolver.Add(firstSchema);

            JSchema4 secondSchema = JSchema4.Parse(second, resolver);

            return secondSchema;
        }

        [Test]
        public void ExtendsDisallowAdditionProperties()
        {
            string json = "{'firstproperty':'blah','secondproperty':'blah2','additional':'blah3','additional2':'blah4'}";

            IList<SchemaValidationEventArgs> errors;
            JSchema4ValidatingReader reader = CreateReader(json, GetExtendedSchema(), out errors);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.StartObject, reader.TokenType);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.PropertyName, reader.TokenType);
            Assert.AreEqual("firstproperty", reader.Value.ToString());
            Assert.AreEqual(1, errors.Count);
            StringAssert.AreEqual(@"Property 'firstproperty' has not been defined and the schema does not allow additional properties. Line 1, position 17.", errors[0].Message);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.String, reader.TokenType);
            Assert.AreEqual("blah", reader.Value.ToString());
            Assert.AreEqual(1, errors.Count);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.PropertyName, reader.TokenType);
            Assert.AreEqual("secondproperty", reader.Value.ToString());
            Assert.AreEqual(1, errors.Count);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.String, reader.TokenType);
            Assert.AreEqual("blah2", reader.Value.ToString());
            Assert.AreEqual(1, errors.Count);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.PropertyName, reader.TokenType);
            Assert.AreEqual("additional", reader.Value.ToString());
            Assert.AreEqual(2, errors.Count);
            Assert.AreEqual("Property 'additional' has not been defined and the schema does not allow additional properties. Line 1, position 62.", errors[1].Message);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.String, reader.TokenType);
            Assert.AreEqual("blah3", reader.Value.ToString());

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.PropertyName, reader.TokenType);
            Assert.AreEqual("additional2", reader.Value.ToString());
            Assert.AreEqual(3, errors.Count);
            Assert.AreEqual("Property 'additional2' has not been defined and the schema does not allow additional properties. Line 1, position 84.", errors[2].Message);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.String, reader.TokenType);
            Assert.AreEqual("blah4", reader.Value.ToString());

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.EndObject, reader.TokenType);

            Assert.IsFalse(reader.Read());
        }

        [Test]
        public void ExtendsMissingRequiredProperties()
        {
            string json = "{}";

            List<string> errors = new List<string>();

            JSchema4ValidatingReader reader = new JSchema4ValidatingReader(new JsonTextReader(new StringReader(json)));
            reader.ValidationEventHandler += (sender, args) => { errors.Add(args.Message); };
            reader.Schema = GetExtendedSchema();

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.StartObject, reader.TokenType);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.EndObject, reader.TokenType);

            Assert.AreEqual(2, errors.Count);
            Assert.AreEqual("AllOf Line 1, position 2.", errors[0]);
            Assert.AreEqual("Required properties are missing from object: secondproperty. Line 1, position 2.", errors[1]);
        }

        [Test]
        public void NoAdditionalItems()
        {
            string schemaJson = @"{
  ""type"":""array"",
  ""items"": [{""type"":""string""},{""type"":""integer""}],
  ""additionalItems"": false
}";

            string json = @"[1, 'a', null]";

            Json.Schema.SchemaValidationEventArgs validationEventArgs = null;

            JSchema4ValidatingReader reader = new JSchema4ValidatingReader(new JsonTextReader(new StringReader(json)));
            reader.ValidationEventHandler += (sender, args) => { validationEventArgs = args; };
            reader.Schema = JSchema4.Parse(schemaJson);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.StartArray, reader.TokenType);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Integer, reader.TokenType);
            Assert.AreEqual("Invalid type. Expected String but got Integer. Line 1, position 2.", validationEventArgs.Message);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.String, reader.TokenType);
            Assert.AreEqual("Invalid type. Expected Integer but got String. Line 1, position 7.", validationEventArgs.Message);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Null, reader.TokenType);
            Assert.AreEqual("Index 3 has not been defined and the schema does not allow additional items. Line 1, position 13.", validationEventArgs.Message);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.EndArray, reader.TokenType);

            Assert.IsFalse(reader.Read());
        }

        [Test]
        public void PatternPropertiesNoAdditionalProperties()
        {
            string schemaJson = @"{
  ""type"":""object"",
  ""patternProperties"": {
     ""hi"": {""type"":""string""},
     ""ho"": {""type"":""string""}
  },
  ""additionalProperties"": false
}";

            string json = @"{
  ""hi"": ""A string!"",
  ""hide"": ""A string!"",
  ""ho"": 1,
  ""hey"": ""A string!""
}";

            Json.Schema.SchemaValidationEventArgs validationEventArgs = null;

            JSchema4ValidatingReader reader = new JSchema4ValidatingReader(new JsonTextReader(new StringReader(json)));
            reader.ValidationEventHandler += (sender, args) => { validationEventArgs = args; };
            reader.Schema = JSchema4.Parse(schemaJson);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.StartObject, reader.TokenType);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.PropertyName, reader.TokenType);
            Assert.AreEqual(null, validationEventArgs);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.String, reader.TokenType);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.PropertyName, reader.TokenType);
            Assert.AreEqual(null, validationEventArgs);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.String, reader.TokenType);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.PropertyName, reader.TokenType);
            Assert.AreEqual(null, validationEventArgs);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.Integer, reader.TokenType);
            Assert.AreEqual("Invalid type. Expected String but got Integer. Line 4, position 10.", validationEventArgs.Message);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.PropertyName, reader.TokenType);
            Assert.AreEqual("Property 'hey' has not been defined and the schema does not allow additional properties. Line 5, position 9.", validationEventArgs.Message);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.String, reader.TokenType);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.EndObject, reader.TokenType);

            Assert.IsFalse(reader.Read());
        }

        [Test]
        public void ExtendedComplex()
        {
            string first = @"{
  ""id"":""first"",
  ""type"":""object"",
  ""properties"":
  {
    ""firstproperty"":{""type"":""string""},
    ""secondproperty"":{""type"":""string"",""maxLength"":10},
    ""thirdproperty"":{
      ""type"":""object"",
      ""properties"":
      {
        ""thirdproperty_firstproperty"":{""type"":""string"",""maxLength"":10,""minLength"":7}
      }
    }
  },
  ""additionalProperties"":{}
}";

            string second = @"{
  ""id"":""second"",
  ""type"":""object"",
  ""extends"":{""$ref"":""first""},
  ""properties"":
  {
    ""secondproperty"":{""type"":""any""},
    ""thirdproperty"":{
      ""extends"":{
        ""properties"":
        {
          ""thirdproperty_firstproperty"":{""maxLength"":9,""minLength"":6,""pattern"":""hi2u""}
        },
        ""additionalProperties"":{""maxLength"":9,""minLength"":6,""enum"":[""one"",""two""]}
      },
      ""type"":""object"",
      ""properties"":
      {
        ""thirdproperty_firstproperty"":{""pattern"":""hi""}
      },
      ""additionalProperties"":{""type"":""string"",""enum"":[""two"",""three""]}
    },
    ""fourthproperty"":{""type"":""string""}
  },
  ""additionalProperties"":false
}";

            JSchemaPreloadedResolver resolver = new JSchemaPreloadedResolver();
            JSchema4 firstSchema = JSchema4.Parse(first);
            resolver.Add(firstSchema);

            JSchema4 secondSchema = JSchema4.Parse(second, resolver);

            string json = @"{
  'firstproperty':'blahblahblahblahblahblah',
  'secondproperty':'secasecasecasecaseca',
  'thirdproperty':{
    'thirdproperty_firstproperty':'aaa',
    'additional':'three'
  }
}";

            SchemaValidationEventArgs validationEventArgs = null;
            List<string> errors = new List<string>();

            JSchema4ValidatingReader reader = new JSchema4ValidatingReader(new JsonTextReader(new StringReader(json)));
            reader.ValidationEventHandler += (sender, args) =>
            {
                validationEventArgs = args;
                errors.Add(validationEventArgs.Path + " - " + validationEventArgs.Message);
            };
            reader.Schema = secondSchema;

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.StartObject, reader.TokenType);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.PropertyName, reader.TokenType);
            Assert.AreEqual("firstproperty", reader.Value.ToString());
            StringAssert.AreEqual(@"firstproperty - Property 'firstproperty' has not been defined and the schema does not allow additional properties. Line 2, position 19.", errors[0]);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.String, reader.TokenType);
            Assert.AreEqual("blahblahblahblahblahblah", reader.Value.ToString());

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.PropertyName, reader.TokenType);
            Assert.AreEqual("secondproperty", reader.Value.ToString());

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.String, reader.TokenType);
            Assert.AreEqual("secasecasecasecaseca", reader.Value.ToString());
            Assert.AreEqual(1, errors.Count);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.PropertyName, reader.TokenType);
            Assert.AreEqual("thirdproperty", reader.Value.ToString());
            Assert.AreEqual(1, errors.Count);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.StartObject, reader.TokenType);
            Assert.AreEqual(1, errors.Count);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.PropertyName, reader.TokenType);
            Assert.AreEqual("thirdproperty_firstproperty", reader.Value.ToString());
            Assert.AreEqual(1, errors.Count);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.String, reader.TokenType);
            Assert.AreEqual("aaa", reader.Value.ToString());
            Assert.AreEqual(2, errors.Count);
            StringAssert.AreEqual(@"thirdproperty.thirdproperty_firstproperty - String 'aaa' does not match regex pattern 'hi'. Line 5, position 40.", errors[1]);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.PropertyName, reader.TokenType);
            Assert.AreEqual("additional", reader.Value.ToString());
            Assert.AreEqual(2, errors.Count);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.String, reader.TokenType);
            Assert.AreEqual("three", reader.Value.ToString());
            Assert.AreEqual(2, errors.Count);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.EndObject, reader.TokenType);
            Assert.AreEqual(3, errors.Count);
            StringAssert.AreEqual(@"thirdproperty - AllOf Line 7, position 4.", errors[2]);

            Assert.IsTrue(reader.Read());
            Assert.AreEqual(JsonToken.EndObject, reader.TokenType);
            Assert.AreEqual(4, errors.Count);
            StringAssert.AreEqual(@" - AllOf Line 8, position 2.", errors[3]);

            Assert.IsFalse(reader.Read());
            Assert.AreEqual(4, errors.Count);
        }

        [Test]
        public void DuplicateErrorsTest()
        {
            string schema = @"{
  ""id"":""ErrorDemo.Database"",
  ""properties"":{
    ""ErrorDemoDatabase"":{
      ""type"":""object"",
      ""required"":true,
      ""properties"":{
        ""URL"":{
          ""type"":""string"",
          ""required"":true
        },
        ""Version"":{
          ""type"":""string"",
          ""required"":true
        },
        ""Date"":{
          ""type"":""string"",
          ""format"":""date-time"",
          ""required"":true
        },
        ""MACLevels"":{
          ""type"":""object"",
          ""required"":true,
          ""properties"":{
            ""MACLevel"":{
              ""type"":""array"",
              ""required"":true,
              ""items"":[
                {
                  ""required"":true,
                  ""properties"":{
                    ""IDName"":{
                      ""type"":""string"",
                      ""required"":true
                    },
                    ""Order"":{
                      ""type"":""string"",
                      ""required"":true
                    },
                    ""IDDesc"":{
                      ""type"":""string"",
                      ""required"":true
                    },
                    ""IsActive"":{
                      ""type"":""string"",
                      ""required"":true
                    }
                  }
                }
              ]
            }
          }
        }
      }
    }
  }
}";

            string json = @"{
  ""ErrorDemoDatabase"":{
    ""URL"":""localhost:3164"",
    ""Version"":""1.0"",
    ""Date"":""6.23.2010, 9:35:18.121"",
    ""MACLevels"":{
      ""MACLevel"":[
        {
          ""@IDName"":""Developer"",
          ""Order"":""0"",
          ""IDDesc"":""DeveloperDesc"",
          ""IsActive"":""True""
        },
        {
          ""IDName"":""Technician"",
          ""Order"":""1"",
          ""IDDesc"":""TechnicianDesc"",
          ""IsActive"":""True""
        },
        {
          ""IDName"":""Administrator"",
          ""Order"":""2"",
          ""IDDesc"":""AdministratorDesc"",
          ""IsActive"":""True""
        },
        {
          ""IDName"":""PowerUser"",
          ""Order"":""3"",
          ""IDDesc"":""PowerUserDesc"",
          ""IsActive"":""True""
        },
        {
          ""IDName"":""Operator"",
          ""Order"":""4"",
          ""IDDesc"":""OperatorDesc"",
          ""IsActive"":""True""
        }
      ]
    }
  }
}";

            IList<SchemaValidationEventArgs> validationEventArgs = new List<SchemaValidationEventArgs>();

            JSchema4ValidatingReader reader = new JSchema4ValidatingReader(new JsonTextReader(new StringReader(json)));
            reader.ValidationEventHandler += (sender, args) => { validationEventArgs.Add(args); };
            reader.Schema = JSchema4.Parse(schema);

            while (reader.Read())
            {
            }

            Assert.AreEqual(1, validationEventArgs.Count);
        }

        [Test]
        public void ReadAsBytes()
        {
            JSchema4 s = new JSchema4Generator().Generate(typeof(byte[]));

            byte[] data = Encoding.UTF8.GetBytes("Hello world");

            JsonReader reader = new JSchema4ValidatingReader(new JsonTextReader(new StringReader(@"""" + Convert.ToBase64String(data) + @"""")))
            {
                Schema = s
            };
            byte[] bytes = reader.ReadAsBytes();

            CollectionAssert.AreEquivalent(data, bytes);
        }

        [Test]
        public void ReadAsInt32()
        {
            JSchema4 s = new JSchema4Generator().Generate(typeof(int));

            JsonReader reader = new JSchema4ValidatingReader(new JsonTextReader(new StringReader(@"1")))
            {
                Schema = s
            };
            int? i = reader.ReadAsInt32();

            Assert.AreEqual(1, i);
        }

        [Test]
        public void ReadAsInt32Failure()
        {
            ExceptionAssert.Throws<JSchemaException>(() =>
            {
                JSchema4 s = new JSchema4Generator().Generate(typeof(int));
                s.Maximum = 2;

                JsonReader reader = new JSchema4ValidatingReader(new JsonTextReader(new StringReader(@"5")))
                {
                    Schema = s
                };
                reader.ReadAsInt32();
            }, "Integer 5 exceeds maximum value of 2. Line 1, position 1.");
        }

        [Test]
        public void ReadAsDecimal()
        {
            JSchema4 s = new JSchema4Generator().Generate(typeof(decimal));

            JsonReader reader = new JSchema4ValidatingReader(new JsonTextReader(new StringReader(@"1.5")))
            {
                Schema = s
            };
            decimal? d = reader.ReadAsDecimal();

            Assert.AreEqual(1.5m, d);
        }

        [Test]
        public void ReadAsDecimalFailure()
        {
            ExceptionAssert.Throws<JSchemaException>(() =>
            {
                JSchema4 s = new JSchema4Generator().Generate(typeof(decimal));
                s.MultipleOf = 1;

                JsonReader reader = new JSchema4ValidatingReader(new JsonTextReader(new StringReader(@"5.5")))
                {
                    Schema = s
                };
                reader.ReadAsDecimal();
            }, "Float 5.5 is not a multiple of 1. Line 1, position 3.");
        }

        [Test]
        public void ReadAsInt32FromSerializer()
        {
            JSchema4ValidatingReader reader = new JSchema4ValidatingReader(new JsonTextReader(new StringReader("[1,2,3]")));
            reader.Schema = new JSchema4Generator().Generate(typeof(int[]));
            int[] values = new JsonSerializer().Deserialize<int[]>(reader);

            Assert.AreEqual(3, values.Length);
            Assert.AreEqual(1, values[0]);
            Assert.AreEqual(2, values[1]);
            Assert.AreEqual(3, values[2]);
        }

        [Test]
        public void ReadAsInt32InArray()
        {
            string schemaJson = @"{
  ""type"":""array"",
  ""items"":{
    ""type"":""integer""
  },
  ""maxItems"":1
}";

            string json = "[1,2]";

            Json.Schema.SchemaValidationEventArgs validationEventArgs = null;

            JSchema4ValidatingReader reader = new JSchema4ValidatingReader(new JsonTextReader(new StringReader(json)));
            reader.ValidationEventHandler += (sender, args) => { validationEventArgs = args; };
            reader.Schema = JSchema4.Parse(schemaJson);

            reader.Read();
            Assert.AreEqual(JsonToken.StartArray, reader.TokenType);

            reader.ReadAsInt32();
            Assert.AreEqual(JsonToken.Integer, reader.TokenType);
            Assert.AreEqual(null, validationEventArgs);

            reader.ReadAsInt32();
            Assert.AreEqual(JsonToken.Integer, reader.TokenType);
            Assert.AreEqual(null, validationEventArgs);

            reader.ReadAsInt32();
            Assert.AreEqual(JsonToken.EndArray, reader.TokenType);
            Assert.AreEqual("Array item count 2 exceeds maximum count of 1. Line 1, position 5.", validationEventArgs.Message);
            Assert.AreEqual("", validationEventArgs.Path);
        }

        [Test]
        public void ReadAsInt32InArrayIncomplete()
        {
            string schemaJson = @"{
  ""type"":""array"",
  ""items"":{
    ""type"":""integer""
  },
  ""maxItems"":1
}";

            string json = "[1,2";

            Json.Schema.SchemaValidationEventArgs validationEventArgs = null;

            JSchema4ValidatingReader reader = new JSchema4ValidatingReader(new JsonTextReader(new StringReader(json)));
            reader.ValidationEventHandler += (sender, args) => { validationEventArgs = args; };
            reader.Schema = JSchema4.Parse(schemaJson);

            reader.Read();
            Assert.AreEqual(JsonToken.StartArray, reader.TokenType);

            reader.ReadAsInt32();
            Assert.AreEqual(JsonToken.Integer, reader.TokenType);
            Assert.AreEqual(null, validationEventArgs);

            reader.ReadAsInt32();
            Assert.AreEqual(JsonToken.Integer, reader.TokenType);
            Assert.AreEqual(null, validationEventArgs);

            reader.ReadAsInt32();
            Assert.AreEqual(JsonToken.None, reader.TokenType);
            Assert.AreEqual(null, validationEventArgs);
        }
    }
}