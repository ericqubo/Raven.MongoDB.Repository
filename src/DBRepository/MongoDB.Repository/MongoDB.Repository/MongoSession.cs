﻿using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MongoDB.Repository
{
    public class MongoSession
    {
        #region 私有方法

        /// <summary>
        /// MongoDB连接字符串默认配置节
        /// </summary>
        private const string DEFAULT_CONFIG_NODE = "MongoDB";
        /// <summary>
        /// Mongo自增长ID数据序列
        /// </summary>
        private MongoSequence _sequence { get; set; }
        /// <summary>
        /// MongoDB WriteConcern
        /// </summary>
        private WriteConcern _writeConcern { get; set; }
        /// <summary>
        /// MongoServer
        /// </summary>
        private MongoServer _mongoServer { get; set; }
        /// <summary>
        /// MongoDatabase
        /// </summary>
        public MongoDatabase mongoDatabase;

        /// <summary>
        /// 根据数据类型得到集合
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <returns></returns>
        private MongoCollection<T> GetCollection<T>() where T : class, new()
        {
            return mongoDatabase.GetCollection<T>(typeof(T).Name);
        }

        /// <summary>
        /// 构造函数
        /// <remarks>默认连接串配置节 Web.config > connectionStrings > MongoDB</remarks>
        /// </summary>
        /// <param name="dbName">数据库名称</param>
        /// <param name="configNode">MongoDB连接字符串配置节</param>
        /// <param name="writeConcern">WriteConcern选项</param>
        /// <param name="sequence">Mongo自增长ID数据序列对象</param>
        /// <param name="isSlaveOK"></param>
        public MongoSession(string dbName, string configNode = DEFAULT_CONFIG_NODE, WriteConcern writeConcern = null, MongoSequence sequence = null, bool isSlaveOK = false, ReadPreference readPreference = null)
        {
            var connString = ConfigurationManager.ConnectionStrings[configNode].ConnectionString;
            this._writeConcern = writeConcern ?? WriteConcern.Unacknowledged;
            this._sequence = sequence ?? new MongoSequence();
            MongoServerSettings serverSettings = new MongoServerSettings();
            this._mongoServer = new MongoServer(serverSettings);

            //this._mongoServer.Settings.WriteConcern = WriteConcern.Unacknowledged;

            if (isSlaveOK)
            {
                var databaseSettings = new MongoDatabaseSettings();
                databaseSettings.WriteConcern = this._writeConcern;
                databaseSettings.ReadPreference = readPreference ?? ReadPreference.SecondaryPreferred;
                this.mongoDatabase = this._mongoServer.GetDatabase(dbName, databaseSettings);
            }
            else
            {
                this.mongoDatabase = this._mongoServer.GetDatabase(dbName, this._writeConcern);
            }
        }

        #endregion


        #region 公有方法

        /// <summary>
        /// 创建自增长ID
        /// <remarks>默认自增ID存放 [Sequence] 集合</remarks>
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <returns></returns>
        public long CreateIncID<T>() where T : class, new()
        {
            long id = 1;
            var collection = mongoDatabase.GetCollection(this._sequence.Sequence);
            var typeName = typeof(T).Name;

            if (collection.Exists() &&
                collection.Find(MongoDB.Driver.Builders.Query.EQ(this._sequence.CollectionName, typeName)).Count() > 0)
            {
                var result = collection.FindAndModify(
                    MongoDB.Driver.Builders.Query.EQ(this._sequence.CollectionName, typeName),
                    null,
                    MongoDB.Driver.Builders.Update.Inc(this._sequence.IncrementID, 1),
                    true);

                if (result.Ok && result.ModifiedDocument != null)
                    long.TryParse(result.ModifiedDocument.GetValue(this._sequence.IncrementID).ToString(), out id);
            }
            else
            {
                collection.Insert(
                    new BsonDocument { 
                        { this._sequence.CollectionName, typeName },
                        { this._sequence.IncrementID, id }
                    },
                    this._writeConcern);
            }

            return id;
        }

        /// <summary>
        /// 查询跟新一条记录后返回该记录
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="query">查询表达式</param>
        /// <param name="sortBy">排序表达式</param>
        /// <param name="update">跟新表达式</param>
        /// <returns></returns>
        public T FindAndModify<T>(IMongoQuery query, IMongoSortBy sortBy = null, IMongoUpdate update = null) where T : class, new()
        {
            T obj = null;

            var result = this.GetCollection<T>().FindAndModify(query, sortBy, update, true);
            if (result.Ok && result.ModifiedDocument != null)
            {
                obj = result.GetModifiedDocumentAs<T>();
            }
            return obj;
        }

        /// <summary>
        /// 创建索引
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="indexKeyArray">索引字段列表</param>
        public void CreateIndex<T>(string[] indexKeyArray) where T : class,new()
        {
            if (indexKeyArray.Length > 0)
            {
                this.GetCollection<T>().CreateIndex(indexKeyArray);
            }
        }

        /// <summary>
        /// 添加数据
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="item">待添加数据</param>
        /// <returns></returns>
        public WriteConcernResult Insert<T>(T item) where T : class, new()
        {
            return this.GetCollection<T>().Insert(item, this._writeConcern);
        }

        /// <summary>
        /// 获取系统当前时间
        /// </summary>
        /// <returns></returns>
        public DateTime GetSysDateTime()
        {
            return mongoDatabase.Eval("new Date()", null).ToUniversalTime();
        }

        /// <summary>
        /// 批量添加数据
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="items">待添加数据集合</param>
        /// <returns></returns>
        public IEnumerable<WriteConcernResult> InsertBatch<T>(IEnumerable<T> items) where T : class, new()
        {
            return this.GetCollection<T>().InsertBatch(items, this._writeConcern);
        }

        /// <summary>
        /// 更新数据对象
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="item">待更新数据对象</param>
        /// <returns></returns>
        public WriteConcernResult Update<T>(T item) where T : class, new()
        {
            return this.GetCollection<T>().Save<T>(item, this._writeConcern);
        }

        /// <summary>
        /// 更新数据
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="query">查询表达式</param>
        /// <param name="update">待更新数据表达式</param>
        /// <param name="updateFlag">修改标志[一条或多条]</param>
        /// <returns></returns>
        public WriteConcernResult Update<T>(IMongoQuery query, IMongoUpdate update, UpdateFlags updateFlag = UpdateFlags.None) where T : class, new()
        {
            return this.GetCollection<T>().Update(query, update, updateFlag, this._writeConcern);
        }

        /// <summary>
        /// 自增长数据
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="query">查询表达式</param>
        /// <param name="name">字段名</param>
        /// <param name="val">自增长的值</param>
        /// <param name="updateFlag">修改标志[一条或多条]</param>
        /// <returns></returns>
        public WriteConcernResult Inc<T>(IMongoQuery query, string name, long val = 1, UpdateFlags updateFlag = UpdateFlags.None) where T : class, new()
        {
            return this.Update<T>(query, MongoDB.Driver.Builders.Update.Inc(name, val), updateFlag);
        }

        /// <summary>
        /// 添加数据至数组
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="query">查询表达式</param>
        /// <param name="name">数组字段名</param>
        /// <param name="val">待添加值</param>
        /// <param name="updateFlag">修改标志[一条或多条]</param>
        /// <returns></returns>
        public WriteConcernResult Push<T>(IMongoQuery query, string name, BsonValue val, UpdateFlags updateFlag = UpdateFlags.None) where T : class, new()
        {
            return this.Update<T>(query, MongoDB.Driver.Builders.Update.Push(name, val), updateFlag);
        }

        /// <summary>
        /// 从数组中删除数据
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="query">查询表达式</param>
        /// <param name="name">数组字段名</param>
        /// <param name="val">待删除值</param>
        /// <param name="updateFlag">修改标志[一条或多条]</param>
        /// <returns></returns>
        public WriteConcernResult Pull<T>(IMongoQuery query, string name, BsonValue val, UpdateFlags updateFlag = UpdateFlags.None) where T : class, new()
        {
            return this.Update<T>(query, MongoDB.Driver.Builders.Update.Pull(name, val), updateFlag);
        }

        /// <summary>
        /// 添加数据至数组(保证数据唯一)
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="query">查询表达式</param>
        /// <param name="name">数组字段名</param>
        /// <param name="val">待添加值</param>
        /// <param name="updateFlag">修改标志[一条或多条]</param>
        /// <returns></returns>
        public WriteConcernResult AddToSet<T>(IMongoQuery query, string name, BsonValue val, UpdateFlags updateFlag = UpdateFlags.None) where T : class, new()
        {
            return this.Update<T>(query, MongoDB.Driver.Builders.Update.AddToSet(name, val), updateFlag);
        }

        /// <summary>
        /// 删除数据
        /// <remarks>一般不用</remarks>
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="query">查询表达式</param>
        /// <param name="removeFlag">删除标志[一条或多条]</param>
        public WriteConcernResult Remove<T>(IMongoQuery query, RemoveFlags removeFlag = RemoveFlags.None) where T : class, new()
        {
            return this.GetCollection<T>().Remove(query, removeFlag, this._writeConcern);
        }

        /// <summary>
        /// 获取多条数据
        /// <remarks>所有或分页数据</remarks>
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="query">查询表达式</param>
        /// <param name="sortBy">排序表达式</param>
        /// <param name="pageIndex">当前页索引</param>
        /// <param name="pageSize">每页数据数</param>
        /// <param name="fields">指定字段表达式</param>
        /// <returns></returns>
        public MongoCursor<T> Query<T>(IMongoQuery query, IMongoSortBy sortBy = null, int pageIndex = 0, int pageSize = 0, IMongoFields fields = null) where T : class, new()
        {
            var cursor = this.GetCollection<T>().Find(query);

            if (fields != null)
                cursor = cursor.SetFields(fields);

            if (sortBy != null)
                cursor = cursor.SetSortOrder(sortBy);
            if (pageSize != 0)
                cursor.SetSkip(pageIndex * pageSize).SetLimit(pageSize);

            return cursor;
        }

        /// <summary>
        /// 获取多条数据
        /// <remarks>所有或分页数据（用于非标准分页）</remarks>
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="start">总条数</param>
        /// <param name="query">查询表达式</param>
        /// <param name="sortBy">排序表达式</param>
        /// <param name="pageIndex">当前页索引</param>
        /// <param name="pageSize">每页数据数</param>
        /// <param name="fields">指定字段表达式</param>
        /// <returns></returns>
        public MongoCursor<T> Query<T>(int start, IMongoQuery query, IMongoSortBy sortBy = null, int pageIndex = 0, int pageSize = 0, IMongoFields fields = null) where T : class, new()
        {
            var cursor = this.GetCollection<T>().Find(query);

            if (fields != null)
                cursor = cursor.SetFields(fields);

            if (sortBy != null)
                cursor = cursor.SetSortOrder(sortBy);
            if (pageSize != 0)
            {
                //如果是第一页，则取start条， 从第二页开始，数据从N+start条开始取
                if (pageIndex != 0)
                {
                    int iCount = (pageIndex - 1) * pageSize + start;
                    cursor.SetSkip(iCount).SetLimit(pageSize);
                }
                else
                {
                    cursor.SetSkip(0).SetLimit(start);
                }
            }
            return cursor;
        }

        /// <summary>
        /// 获取前几条数据
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="query">查询表达式</param>
        /// <param name="sortBy">排序表达式</param>
        /// <param name="topCount">数据条数</param>
        /// <param name="fields">指定字段表达式</param>
        /// <returns></returns>
        public MongoCursor<T> Top<T>(IMongoQuery query, IMongoSortBy sortBy = null, int topCount = 10, IMongoFields fields = null) where T : class, new()
        {
            // return this.Query<T>(query, sortBy, pageSize: topCount);
            return this.Query<T>(query, sortBy, 0, topCount, fields);
        }

        /// <summary>
        /// 获取一条数据
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="query">查询表达式</param>
        /// <param name="sortBy">排序表达式</param>
        /// <param name="fields">字段</param>
        /// <returns></returns>
        public T Get<T>(IMongoQuery query, IMongoSortBy sortBy = null, IMongoFields fields = null) where T : class, new()
        {
            T obj = null;

            //foreach (var item in this.Top<T>(query, sortBy, topCount: 1))
            //foreach (var item in this.Top<T>(query, sortBy, 1, fields))
            //    obj = item;
            obj = this.Top<T>(query, sortBy, 1, fields).FirstOrDefault();
            return obj;
        }

        /// <summary>
        /// Distinct数据
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="key">字段名</param>
        /// <param name="query">查询表达式</param>
        /// <returns></returns>
        public IEnumerable<BsonValue> Distinct<T>(string key, IMongoQuery query) where T : class, new()
        {
            return this.GetCollection<T>().Distinct(key, query);
        }

        /// <summary>
        /// 获取数据数
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="query">查询表达式</param>
        /// <returns></returns>
        public long Count<T>(IMongoQuery query) where T : class, new()
        {
            return this.GetCollection<T>().Count(query);
        }

        /// <summary>
        /// 二维空间搜索最近的数据
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="query">查询表达式</param>
        /// <param name="x">坐标X</param>
        /// <param name="y">坐标Y</param>
        /// <param name="limit">数据条数</param>
        /// <param name="geoNearOptions">geoNearOptions选项</param>
        /// <returns></returns>
        public GeoNearResult<T> GeoNear<T>(IMongoQuery query, double x, double y, int limit = 1, IMongoGeoNearOptions geoNearOptions = null) where T : class, new()
        {
            #region Command Demo

            //> db.runCommand({geoNear:"asdf", near:[50,50]})  
            //{  
            //    "ns" : "test.places",  
            //    "near" : "1100110000001111110000001111110000001111110000001111",  
            //    "results" : [  
            //            {  
            //                    "dis" : 69.29646421910687,  
            //                    "obj" : {  
            //                            "_id" : ObjectId("4b8bd6b93b83c574d8760280"),  
            //                            "y" : [  
            //                                    1,  
            //                                    1  
            //                            ],  
            //                            "category" : "Coffee"  
            //                    }  
            //            },  
            //            {  
            //                    "dis" : 69.29646421910687,  
            //                    "obj" : {  
            //                            "_id" : ObjectId("4b8bd6b03b83c574d876027f"),  
            //                            "y" : [  
            //                                    1,  
            //                                    1  
            //                            ]  
            //                    }  
            //            }  
            //    ],  
            //    "stats" : {  
            //            "time" : 0,  
            //            "btreelocs" : 1,  
            //            "btreelocs" : 1,  
            //            "nscanned" : 2,  
            //            "nscanned" : 2,  
            //            "objectsLoaded" : 2,  
            //            "objectsLoaded" : 2,  
            //            "avgDistance" : 69.29646421910687  
            //    },  
            //    "ok" : 1  
            //}  

            #endregion

            return this.GetCollection<T>().GeoNear(query, x, y, limit, geoNearOptions);
        }


        /// <summary>
        /// Mapreduce
        /// </summary>
        public MapReduceResult Mapreduce<T>(IMongoQuery query, BsonJavaScript map, BsonJavaScript reduce) where T : class, new()
        {
            MapReduceArgs args = new MapReduceArgs();
            args.Query = query;
            args.MapFunction = map;
            args.ReduceFunction = reduce;

            return this.GetCollection<T>().MapReduce(args);
        }

        /// <summary>
        /// Mapreduce
        /// </summary>
        public MapReduceResult Mapreduce<T>(IMongoQuery query, BsonJavaScript map, BsonJavaScript reduce, MapReduceArgs args) where T : class, new()
        {
            return this.GetCollection<T>().MapReduce(args);
        }

        /// <summary>
        /// 创建2d索引
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="indexKey"></param>
        public void Create2DIndex<T>(string indexKey) where T : class,new()
        {
            if (!string.IsNullOrEmpty(indexKey))
            {
                this.GetCollection<T>().EnsureIndex(IndexKeys.GeoSpatial(indexKey));
            }
        }

        #endregion
    }
}