﻿/* Copyright 2023-present MongoDB Inc.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace MongoDB.EntityFrameworkCore.FunctionalTests.Metadata.Conventions.BsonAttributes;

[XUnitCollection("ConventionsTests")]
public class BsonElementAttributeConventionTests(TemporaryDatabaseFixture tempDatabase)
    : IClassFixture<TemporaryDatabaseFixture>
{
    class IntendedStorageEntity
    {
        public ObjectId _id { get; set; }

        public string name { get; set; }
    }

    class NonKeyRemappingEntity
    {
        public ObjectId _id { get; set; }

        [BsonElement("name")]
        public string? RemapThisToName { get; set; }
    }

    class KeyRemappingEntity
    {
        [BsonElement("_id")]
        public ObjectId _id { get; set; }

        public string? name { get; set; }
    }

    [Fact]
    public void BsonElementAttribute_redefines_element_name_for_insert_and_query()
    {
        var collection = tempDatabase.CreateTemporaryCollection<NonKeyRemappingEntity>();

        var id = ObjectId.GenerateNewId();
        const string name = "The quick brown fox";

        {
            var dbContext = SingleEntityDbContext.Create(collection);
            dbContext.Entitites.Add(new NonKeyRemappingEntity
            {
                _id = id, RemapThisToName = name
            });
            dbContext.SaveChanges();
        }

        {
            var actual = collection.Database.GetCollection<IntendedStorageEntity>(collection.CollectionNamespace.CollectionName);
            var directFound = actual.Find(f => f._id == id).Single();
            Assert.Equal(name, directFound.name);
        }
    }

    [Fact]
    public void BsonElementAttribute_redefines_key_name_for_insert_and_query()
    {
        var collection = tempDatabase.CreateTemporaryCollection<KeyRemappingEntity>();

        var id = ObjectId.GenerateNewId();
        const string name = "The quick brown fox";

        {
            var dbContext = SingleEntityDbContext.Create(collection);
            dbContext.Entitites.Add(new KeyRemappingEntity
            {
                _id = id, name = name
            });
            dbContext.SaveChanges();
        }

        {
            var actual = collection.Database.GetCollection<IntendedStorageEntity>(collection.CollectionNamespace.CollectionName);
            var directFound = actual.Find(f => f._id == id).Single();
            Assert.Equal(name, directFound.name);
        }
    }

    [Fact]
    public void BsonElementAttribute_redefines_key_name_for_delete()
    {
        var collection = tempDatabase.CreateTemporaryCollection<KeyRemappingEntity>();

        var id = ObjectId.GenerateNewId();
        const string name = "The quick brown fox";

        {
            var dbContext = SingleEntityDbContext.Create(collection);
            var entity = new KeyRemappingEntity
            {
                _id = id, name = name
            };
            dbContext.Entitites.Add(entity);
            dbContext.SaveChanges();

            dbContext.Entitites.Remove(entity);
            dbContext.SaveChanges();
        }

        {
            var actual = collection.Database.GetCollection<IntendedStorageEntity>(collection.CollectionNamespace.CollectionName);
            Assert.Equal(0, actual.AsQueryable().Count());
        }
    }
}