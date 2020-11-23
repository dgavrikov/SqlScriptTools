using Microsoft.Extensions.Logging;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using Microsoft.SqlServer.Management.Smo.Agent;
using Microsoft.SqlServer.Management.Smo.Broker;
using SqlScriptTools.Generator.Abstractions;
using SqlScriptTools.Generator.Settings;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SqlScriptTools.Generator.Services.MsSql
{
    internal class MssqlScriptService: IScriptService
    {
        private readonly ConnectionInfo _connectionInfo;
        private readonly string[] _schemaExcluded = new[] { "sys", "information_schema" };
        private readonly ScriptingOptions _scriptingOption;
        private readonly ILogger<MssqlScriptService> _logger;

        private static ServerConnection GetConnection(ConnectionInfo connectionInfo)
        {
            if (connectionInfo == null)
                throw new ArgumentNullException(nameof(connectionInfo));

            var serverConnection = new ServerConnection(connectionInfo.Server)
            {
                DatabaseName = "master"
            };
            if (!string.IsNullOrEmpty(connectionInfo.Login))
            {
                serverConnection.Login = connectionInfo.Login;
                serverConnection.Password = connectionInfo.Password;
            }
            return serverConnection;
        }

        private static string GetServerName(Server server)
            => server.NetName;

        public MssqlScriptService(
            ConnectionInfo connectionInfo,
            ILogger<MssqlScriptService> logger)
        {
            _logger = logger;
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            _connectionInfo = connectionInfo;
            _scriptingOption = new ScriptingOptions
            {
                AllowSystemObjects = false,
                ScriptBatchTerminator = true,
                Default = true,
                Encoding = Encoding.UTF8,
                ExtendedProperties = true
            };
        }

        public Task<List<IScriptInfo>> GetScriptInfoAsync()
        {
            const string method = nameof(GetScriptInfoAsync);
            _logger?.LogInformation($"{method} begin.");

            var listScriptInfo = new List<IScriptInfo>(30 * _connectionInfo.Databases.Length);
            var tasks = new List<Task<List<IScriptInfo>>>(_connectionInfo.Databases.Length);

            foreach (var database in _connectionInfo.Databases)
            {
                var task = Task.Run(() =>
                {
                    var taskListScriptInfo = new List<IScriptInfo>(30);
                    
                    var connection = GetConnection(_connectionInfo);
                    connection.DatabaseName = database;

                    var server = new Server(connection);

                    taskListScriptInfo.AddRange(GetEndpoints(server));
                    taskListScriptInfo.AddRange(GetServerJob(server));

                    taskListScriptInfo.AddRange(GetSchemas(server, database));
                    taskListScriptInfo.AddRange(GetTables(server, database));
                    taskListScriptInfo.AddRange(GetViews(server, database));
                    taskListScriptInfo.AddRange(GetSynonyms(server, database));
                    taskListScriptInfo.AddRange(GetStoredProcedures(server, database));
                    taskListScriptInfo.AddRange(GetUserDefinedAggregates(server, database));
                    taskListScriptInfo.AddRange(GetUserDefinedDataTypes(server, database));
                    taskListScriptInfo.AddRange(GetUserDefinedFunctions(server, database));
                    taskListScriptInfo.AddRange(GetUserDefinedTableTypes(server, database));
                    taskListScriptInfo.AddRange(GetUserDefinedTypes(server, database));
                    // 2005
                    if (server.VersionMajor >= 9)
                    {
                        taskListScriptInfo.AddRange(GetAssemblies(server, database));
                        taskListScriptInfo.AddRange(GetPartitionFunctions(server, database));
                        taskListScriptInfo.AddRange(GetPartitionSchemes(server, database));
                        taskListScriptInfo.AddRange(GetServiceBrokerMessageTypes(server, database));
                        taskListScriptInfo.AddRange(GetServiceBrokerServiceContracts(server, database));
                        taskListScriptInfo.AddRange(GetServiceBrokerQueues(server, database));
                        taskListScriptInfo.AddRange(GetServiceBrokerServices(server, database));
                        taskListScriptInfo.AddRange(GetServiceBrokerRoutes(server, database));
                        taskListScriptInfo.AddRange(GetServiceBrokerRemoteBinding(server, database));
                    }
                    //2012
                    if (server.VersionMajor >= 11)
                    {
                        taskListScriptInfo.AddRange(GetSequences(server, database));
                    }

                    return taskListScriptInfo;
                });
                tasks.Add(task);
            }

            Task.WaitAll(tasks.ToArray());
            
            foreach(var task in tasks)
            {
                if (task.IsCompletedSuccessfully)
                    listScriptInfo.AddRange(task.Result);
                else if (task.Exception != null)
                {
                    _logger?.LogError(task.Exception, $"Error task status={task.Status}");
                }
            }

            _logger?.LogInformation($"{method} end.");
            return Task.FromResult(listScriptInfo);
        }

        #region private generate ScriptInfo server level

        private List<MssqlScriptInfo> GetEndpoints(Server server)
        {
            const string method = nameof(GetEndpoints);
            _logger?.LogInformation($"{method} on {server} begin.");

            var scriptCollection = new List<MssqlScriptInfo>(server.Endpoints.Count);
            
            foreach (Endpoint ep in server.Endpoints)
            {
                if (ep.IsSystemObject)
                    continue;

                var scriptInfo = new MssqlScriptInfo
                {
                    Location = new ScriptInfoLocation { ServerName = GetServerName(server) },
                    Type = "Endpoint",
                    Schema = ep.Owner,
                    Name = ep.Name,
                    Body = MapToString(ep.Script(_scriptingOption))
                };
                scriptCollection.Add(scriptInfo);
            }

            _logger?.LogInformation($"{method} on {server} end.");
            return scriptCollection;
        }
        private List<MssqlScriptInfo> GetServerJob(Server server)
        {
            const string method = nameof(GetServerJob);
            _logger?.LogInformation($"{method} on {server} begin.");
            var scriptCollection = new List<MssqlScriptInfo>(server.JobServer.Jobs.Count);

            foreach (Job job in server.JobServer.Jobs)
            {
                var scriptInfo = new MssqlScriptInfo
                {
                    Location = new ScriptInfoLocation { ServerName = GetServerName(server) },
                    Type = "Job",
                    Schema = job.Category,
                    Name = job.Name,
                    Body = MapToString(job.Script(_scriptingOption))
                };
                scriptCollection.Add(scriptInfo);

                // Steps
                foreach(JobStep step in job.JobSteps)
                {
                    var stepScriptInfo = new MssqlScriptInfo
                    {
                        Location = new ScriptInfoLocation { ServerName = GetServerName(server) },
                        Type = "JobStep",
                        Name = step.Name,
                        Schema = job.Name,
                        Body = MapToString(step.Script(_scriptingOption))
                    };
                    scriptCollection.Add(stepScriptInfo);
                }

                // Scheduller
                foreach(JobSchedule schedule in job.JobSchedules)
                {
                    var schedullerScriptInfo = new MssqlScriptInfo
                    {
                        Location = new ScriptInfoLocation { ServerName = GetServerName(server) },
                        Type = "JobStep",
                        Name = schedule.Name,
                        Schema = job.Name,
                        Body = MapToString(schedule.Script(_scriptingOption))
                    };
                    scriptCollection.Add(schedullerScriptInfo);
                }
            }
            _logger?.LogInformation($"{method} on {server} end.");
            return scriptCollection;
        }
        #endregion

        #region private generate ScriptInfo database level
        private List<MssqlScriptInfo> GetSchemas(Server server, string databaseName)
        {
            const string method = nameof(GetSchemas);
            _logger?.LogInformation($"{method} on {server}({databaseName}) begin.");

            var database = server.Databases[databaseName];
            var scriptCollection = new List<MssqlScriptInfo>(database.Schemas.Count);

            foreach (Schema schema in database.Schemas)
            {
                if (schema.IsSystemObject)
                    continue;

                var scriptInfo = new MssqlScriptInfo
                {
                    Location = new ScriptInfoLocation { ServerName = GetServerName(server), DatabaseName = databaseName },
                    Type = "Schema",
                    Schema = schema.Owner,
                    Name = schema.Name,
                    Body = MapToString(schema.Script(_scriptingOption))
                };
                scriptCollection.Add(scriptInfo);
            }

            _logger?.LogInformation($"{method} on {server}({databaseName}) end.");
            return scriptCollection;

        }
        private List<MssqlScriptInfo> GetPartitionSchemes(Server server, string databaseName)
        {
            const string method = nameof(GetPartitionSchemes);
            _logger?.LogInformation($"{method} on {server}({databaseName}) begin.");

            var database = server.Databases[databaseName];
            var scriptCollection = new List<MssqlScriptInfo>(database.PartitionSchemes.Count);

            foreach (PartitionScheme ps in database.PartitionSchemes)
            {
                var scriptInfo = new MssqlScriptInfo
                {
                    Location = new ScriptInfoLocation { ServerName = GetServerName(server), DatabaseName = databaseName },
                    Type = "PartitionScheme",
                    Name = ps.Name,
                    Body = MapToString(ps.Script(_scriptingOption))
                };
                scriptCollection.Add(scriptInfo);
            }

            _logger?.LogInformation($"{method} on {server}({databaseName}) end.");

            return scriptCollection;

        }
        private List<MssqlScriptInfo> GetPartitionFunctions(Server server, string databaseName)
        {
            const string method = nameof(GetPartitionFunctions);
            _logger?.LogInformation($"{method} on {server}({databaseName}) begin.");

            var database = server.Databases[databaseName];
            var scriptCollection = new List<MssqlScriptInfo>(database.PartitionFunctions.Count);

            foreach (PartitionFunction pf in database.PartitionFunctions)
            {

                var scriptInfo = new MssqlScriptInfo
                {
                    Location = new ScriptInfoLocation { ServerName = GetServerName(server), DatabaseName = databaseName },
                    Type = "PartitionFunction",
                    Name = pf.Name,
                    Body = MapToString(pf.Script(_scriptingOption))
                };
                scriptCollection.Add(scriptInfo);
            }
            _logger?.LogInformation($"{method} on {server}({databaseName}) end.");

            return scriptCollection;
        }
        private List<MssqlScriptInfo> GetTables(Server server, string databaseName)
        {
            const string method = nameof(GetTables);
            _logger?.LogInformation($"{method} on {server}({databaseName}) begin.");

            var database = server.Databases[databaseName];
            var scriptCollection = new List<MssqlScriptInfo>(database.Tables.Count);

            foreach (Table table in database.Tables)
            {
                if (table.IsSystemObject)
                    continue;

                var tableScript = new StringBuilder();

                // Tables
                if (_schemaExcluded.Contains(table.Schema.ToLower()))
                    continue;
                if (table.IsSystemObject)
                    continue;

                tableScript.AppendLine(MapToString(table.Script(_scriptingOption)));

                // Check constraint
                foreach (Check check in table.Checks)
                    tableScript.AppendLine(MapToString(check.Script(_scriptingOption)));

                // Foreign Key
                foreach (ForeignKey foreignKey in table.ForeignKeys)
                {
                    var foreignKeyScript = new StringBuilder();
                    foreignKeyScript.AppendLine(MapToString(foreignKey.Script(_scriptingOption)));
                    var scriptForeignKeyInfo = new MssqlScriptInfo
                    {
                        Location = new ScriptInfoLocation { ServerName = GetServerName(server), DatabaseName = databaseName },
                        Type = "ForeignKey",
                        Schema = $"{table.Schema}.{table.Name}",
                        Name = foreignKey.Name,
                        Body = foreignKeyScript.ToString()
                    };
                    scriptCollection.Add(scriptForeignKeyInfo);
                }
                
                foreach(Trigger trigger in table.Triggers)
                {
                    var triggerScript = new StringBuilder();
                    triggerScript.AppendLine(MapToString(trigger.Script(_scriptingOption)));
                    var scriptIndexInfo = new MssqlScriptInfo
                    {
                        Location = new ScriptInfoLocation { ServerName = GetServerName(server), DatabaseName = databaseName },
                        Type = "Trigger",
                        Schema = $"{table.Schema}.{table.Name}",
                        Name = trigger.Name,
                        Body = triggerScript.ToString()
                    };
                    scriptCollection.Add(scriptIndexInfo);
                }

                // Index
                foreach(Microsoft.SqlServer.Management.Smo.Index index in table.Indexes)
                {
                    if (index.IsClustered)
                    {
                        tableScript.AppendLine(MapToString(index.Script(_scriptingOption)));
                        continue;
                    }

                    var indexScript = new StringBuilder();
                    indexScript.AppendLine(MapToString(index.Script(_scriptingOption)));
                    var scriptIndexInfo = new MssqlScriptInfo
                    {
                        Location = new ScriptInfoLocation { ServerName = GetServerName(server), DatabaseName =databaseName },
                        Type = "Index",
                        Schema = $"{table.Schema}.{table.Name}",
                        Name = index.Name,
                        Body = indexScript.ToString()
                    };
                    scriptCollection.Add(scriptIndexInfo);
                }                    

                var scriptTableInfo = new MssqlScriptInfo
                {
                    Location = new ScriptInfoLocation { ServerName = GetServerName(server), DatabaseName = databaseName },
                    Type = "Table",
                    Schema = table.Schema,
                    Name = table.Name,
                    Body = tableScript.ToString()
                };
                scriptCollection.Add(scriptTableInfo);

            }
            _logger?.LogInformation($"{method} on {server}({databaseName}) end.");

            return scriptCollection;
        }
        private List<MssqlScriptInfo> GetViews(Server server, string databaseName)
        {
            const string method = nameof(GetViews);
            _logger?.LogInformation($"{method} on {server}({databaseName}) begin.");

            var database = server.Databases[databaseName];
            var scriptCollection = new List<MssqlScriptInfo>(database.Views.Count);

            foreach (View view in database.Views)
            {
                if (view.IsSystemObject)
                    continue;

                if (_schemaExcluded.Contains(view.Schema.ToLower()))
                    continue;

                var scriptInfo = new MssqlScriptInfo
                {
                    Location = new ScriptInfoLocation { ServerName = GetServerName(server), DatabaseName = databaseName },
                    Type = "View",
                    Schema = view.Schema,
                    Name = view.Name,
                    Body = MapToString(view.Script(_scriptingOption))
                };
                scriptCollection.Add(scriptInfo);
            }
            _logger?.LogInformation($"{method} on {server}({databaseName}) end.");
            return scriptCollection;

        }
        private List<MssqlScriptInfo> GetSynonyms(Server server, string databaseName)
        {
            const string method = nameof(GetSynonyms);
            _logger?.LogInformation($"{method} on {server}({databaseName}) begin.");

            var database = server.Databases[databaseName];
            var scriptCollection = new List<MssqlScriptInfo>(database.Synonyms.Count);

            foreach (Synonym synonym in database.Synonyms)
            {
                if (_schemaExcluded.Contains(synonym.Schema.ToLower()))
                    continue;

                var scriptInfo = new MssqlScriptInfo
                {
                    Location = new ScriptInfoLocation { ServerName = GetServerName(server), DatabaseName = databaseName },
                    Type = "Synonym",
                    Schema = synonym.Schema,
                    Name = synonym.Name,
                    Body = MapToString(synonym.Script(_scriptingOption))
                };
                scriptCollection.Add(scriptInfo);
            }

            _logger?.LogInformation($"{method} on {server}({databaseName}) end.");

            return scriptCollection;

        }
        private List<MssqlScriptInfo> GetSequences(Server server, string databaseName)
        {
            const string method = nameof(GetSequences);
            _logger?.LogInformation($"{method} on {server}({databaseName}) begin.");

            var database = server.Databases[databaseName];
            var scriptCollection = new List<MssqlScriptInfo>(database.Sequences.Count);

            foreach (Sequence sequence in database.Sequences)
            {
                if (_schemaExcluded.Contains(sequence.Schema.ToLower()))
                    continue;

                var scriptInfo = new MssqlScriptInfo
                {
                    Location = new ScriptInfoLocation { ServerName = GetServerName(server), DatabaseName = databaseName },
                    Type = "Sequence",
                    Schema = sequence.Schema,
                    Name = sequence.Name,
                    Body = MapToString(sequence.Script(_scriptingOption))
                };
                scriptCollection.Add(scriptInfo);
            }

            _logger?.LogInformation($"{method} on {server}({databaseName}) end.");

            return scriptCollection;

        }
        private List<MssqlScriptInfo> GetAssemblies(Server server, string databaseName)
        {
            const string method = nameof(GetAssemblies);
            _logger?.LogInformation($"{method} on {server}({databaseName}) begin.");

            var database = server.Databases[databaseName];
            var scriptCollection = new List<MssqlScriptInfo>(database.Assemblies.Count);

            foreach (SqlAssembly assembly in database.Assemblies)
            {
                if (assembly.IsSystemObject)
                    continue;

                var scriptInfo = new MssqlScriptInfo
                {
                    Location = new ScriptInfoLocation { ServerName = GetServerName(server), DatabaseName = databaseName },
                    Type = "Assembly",
                    Name = assembly.Name,
                    Body = MapToString(assembly.Script(_scriptingOption))
                };
                scriptCollection.Add(scriptInfo);
            }

            _logger?.LogInformation($"{method} on {server}({databaseName}) end.");
            return scriptCollection;

        }
        private List<MssqlScriptInfo> GetStoredProcedures(Server server, string databaseName)
        {
            const string method = nameof(GetStoredProcedures);
            _logger?.LogInformation($"{method} on {server}({databaseName}) begin.");

            var database = server.Databases[databaseName];
            var scriptCollection = new List<MssqlScriptInfo>(database.StoredProcedures.Count);

            foreach (StoredProcedure sp in database.StoredProcedures)
            {
                if (sp.IsSystemObject)
                    continue;
                if (_schemaExcluded.Contains(sp.Schema.ToLower()))
                    continue;

                var scriptInfo = new MssqlScriptInfo
                {
                    Location = new ScriptInfoLocation { ServerName = GetServerName(server), DatabaseName = databaseName },
                    Type = "StoredProcedure",
                    Schema = sp.Schema,
                    Name = sp.Name,
                    Body = MapToString(sp.Script(_scriptingOption))
                };
                scriptCollection.Add(scriptInfo);
            }
            _logger?.LogInformation($"{method} on {server}({databaseName}) end.");
            return scriptCollection;

        }
        private List<MssqlScriptInfo> GetUserDefinedAggregates(Server server, string databaseName)
        {
            const string method = nameof(GetUserDefinedAggregates);
            _logger?.LogInformation($"{method} on {server}({databaseName}) begin.");

            var database = server.Databases[databaseName];
            var scriptCollection = new List<MssqlScriptInfo>(database.UserDefinedAggregates.Count);

            foreach (UserDefinedAggregate udAgr in database.UserDefinedAggregates)
            {
                if (_schemaExcluded.Contains(udAgr.Schema.ToLower()))
                    continue;

                var scriptInfo = new MssqlScriptInfo
                {
                    Location = new ScriptInfoLocation { ServerName = GetServerName(server), DatabaseName = databaseName },
                    Type = "UserDefinedAggregate",
                    Schema = udAgr.Schema,
                    Name = udAgr.Name,
                    Body = MapToString(udAgr.Script(_scriptingOption))
                };
                scriptCollection.Add(scriptInfo);
            }

            _logger?.LogInformation($"{method} on {server}({databaseName}) end.");
            return scriptCollection;

        }
        private List<MssqlScriptInfo> GetUserDefinedDataTypes(Server server, string databaseName)
        {
            const string method = nameof(GetUserDefinedDataTypes);
            _logger?.LogInformation($"{method} on {server}({databaseName}) begin.");

            var database = server.Databases[databaseName];
            var scriptCollection = new List<MssqlScriptInfo>(database.UserDefinedDataTypes.Count);

            foreach (UserDefinedDataType uddt in database.UserDefinedDataTypes)
            {
                if (_schemaExcluded.Contains(uddt.Schema.ToLower()))
                    continue;

                var scriptInfo = new MssqlScriptInfo
                {
                    Location = new ScriptInfoLocation { ServerName = GetServerName(server), DatabaseName = databaseName },
                    Type = "UserDefinedDataType",
                    Schema = uddt.Schema,
                    Name = uddt.Name,
                    Body = MapToString(uddt.Script(_scriptingOption))
                };
                scriptCollection.Add(scriptInfo);
            }

            _logger?.LogInformation($"{method} on {server}({databaseName}) end.");
            return scriptCollection;

        }
        private List<MssqlScriptInfo> GetUserDefinedFunctions(Server server, string databaseName)
        {
            const string method = nameof(GetUserDefinedFunctions);
            _logger?.LogInformation($"{method} on {server}({databaseName}) begin.");

            var database = server.Databases[databaseName];
            var scriptCollection = new List<MssqlScriptInfo>(database.UserDefinedFunctions.Count);

            foreach (UserDefinedFunction udf in database.UserDefinedFunctions)
            {
                if (udf.IsSystemObject)
                    continue;
                if (_schemaExcluded.Contains(udf.Schema.ToLower()))
                    continue;

                var scriptInfo = new MssqlScriptInfo
                {
                    Location = new ScriptInfoLocation { ServerName = GetServerName(server), DatabaseName = databaseName },
                    Type = "UserDefinedFunction",
                    Schema = udf.Schema,
                    Name = udf.Name,
                    Body = MapToString(udf.Script(_scriptingOption))
                };
                scriptCollection.Add(scriptInfo);
            }

            _logger?.LogInformation($"{method} on {server}({databaseName}) end.");
            return scriptCollection;
        }
        private List<MssqlScriptInfo> GetUserDefinedTableTypes(Server server, string databaseName)
        {
            const string method = nameof(GetUserDefinedTableTypes);
            _logger?.LogInformation($"{method} on {server}({databaseName}) begin.");

            var database = server.Databases[databaseName];
            var scriptCollection = new List<MssqlScriptInfo>(database.UserDefinedTableTypes.Count);

            foreach (UserDefinedTableType udtt in database.UserDefinedTableTypes)
            {
                if (_schemaExcluded.Contains(udtt.Schema.ToLower()))
                    continue;

                var scriptInfo = new MssqlScriptInfo
                {
                    Location = new ScriptInfoLocation { ServerName = GetServerName(server), DatabaseName = databaseName },
                    Type = "UserDefinedTableType",
                    Schema = udtt.Schema,
                    Name = udtt.Name,
                    Body = MapToString(udtt.Script(_scriptingOption))
                };
                scriptCollection.Add(scriptInfo);
            }

            _logger?.LogInformation($"{method} on {server}({databaseName}) end.");
            return scriptCollection;

        }
        private List<MssqlScriptInfo> GetUserDefinedTypes(Server server, string databaseName)
        {
            const string method = nameof(GetUserDefinedTypes);
            _logger?.LogInformation($"{method} on {server}({databaseName}) begin.");

            var database = server.Databases[databaseName];
            var scriptCollection = new List<MssqlScriptInfo>(database.UserDefinedTypes.Count);

            foreach (UserDefinedType udt in database.UserDefinedTypes)
            {
                if (_schemaExcluded.Contains(udt.Schema.ToLower()))
                    continue;

                var scriptInfo = new MssqlScriptInfo
                {
                    Location = new ScriptInfoLocation { ServerName = GetServerName(server), DatabaseName = databaseName },
                    Type = "UserDefinedTableType",
                    Schema = udt.Schema,
                    Name = udt.Name,
                    Body = MapToString(udt.Script(_scriptingOption))
                };
                scriptCollection.Add(scriptInfo);
            }

            _logger?.LogInformation($"{method} on {server}({databaseName}) end.");
            return scriptCollection;

        }
        private List<MssqlScriptInfo> GetServiceBrokerMessageTypes(Server server, string databaseName)
        {
            const string method = nameof(GetServiceBrokerMessageTypes);
            _logger?.LogInformation($"{method} on {server}({databaseName}) begin.");

            var database = server.Databases[databaseName];
            var scriptCollection = new List<MssqlScriptInfo>(database.ServiceBroker.MessageTypes.Count);

            foreach (MessageType mt in database.ServiceBroker.MessageTypes)
            {
                if (mt.IsSystemObject)
                    continue;
                var scriptInfo = new MssqlScriptInfo
                {
                    Location = new ScriptInfoLocation { ServerName = GetServerName(server), DatabaseName = databaseName },
                    Type = "ServiceBrokerMessageType",
                    Name = mt.Name,
                    Body = MapToString(mt.Script(_scriptingOption))
                };
                scriptCollection.Add(scriptInfo);
            }

            _logger?.LogInformation($"{method} on {server}({databaseName}) end.");
            return scriptCollection;

        }
        private List<MssqlScriptInfo> GetServiceBrokerServiceContracts(Server server, string databaseName)
        {
            const string method = nameof(GetServiceBrokerServiceContracts);
            _logger?.LogInformation($"{method} on {server}({databaseName}) begin.");

            var database = server.Databases[databaseName];
            var scriptCollection = new List<MssqlScriptInfo>(database.ServiceBroker.ServiceContracts.Count);

            foreach (ServiceContract sc in database.ServiceBroker.ServiceContracts)
            {
                if (sc.IsSystemObject)
                    continue;
                var scriptInfo = new MssqlScriptInfo
                {
                    Location = new ScriptInfoLocation { ServerName = GetServerName(server), DatabaseName = databaseName },
                    Type = "ServiceBrokerServiceContract",
                    Name = sc.Name,
                    Body = MapToString(sc.Script(_scriptingOption))
                };
                scriptCollection.Add(scriptInfo);
            }

            _logger?.LogInformation($"{method} on {server}({databaseName}) end.");
            return scriptCollection;

        }
        private List<MssqlScriptInfo> GetServiceBrokerQueues(Server server, string databaseName)
        {
            const string method = nameof(GetServiceBrokerQueues);
            _logger?.LogInformation($"{method} on {server}({databaseName}) begin.");

            var database = server.Databases[databaseName];
            var scriptCollection = new List<MssqlScriptInfo>(database.ServiceBroker.Queues.Count);

            foreach (ServiceQueue sq in database.ServiceBroker.Queues)
            {
                if (sq.IsSystemObject)
                    continue;
                if (_schemaExcluded.Contains(sq.Schema.ToLower()))
                    continue;

                var scriptInfo = new MssqlScriptInfo
                {
                    Location = new ScriptInfoLocation { ServerName = GetServerName(server), DatabaseName = databaseName },
                    Type = "ServiceBrokerQueues",
                    Name = sq.Name,
                    Schema = sq.Schema,
                    Body = MapToString(sq.Script(_scriptingOption))
                };
                scriptCollection.Add(scriptInfo);
            }

            _logger?.LogInformation($"{method} on {server}({databaseName}) end.");
            return scriptCollection;
        }
        private List<MssqlScriptInfo> GetServiceBrokerServices(Server server, string databaseName)
        {
            const string method = nameof(GetServiceBrokerServices);
            _logger?.LogInformation($"{method} on {server}({databaseName}) begin.");

            var database = server.Databases[databaseName];
            var scriptCollection = new List<MssqlScriptInfo>(database.ServiceBroker.Services.Count);

            foreach (BrokerService bs in database.ServiceBroker.Services)
            {
                if (bs.IsSystemObject)
                    continue;

                var scriptInfo = new MssqlScriptInfo
                {
                    Location = new ScriptInfoLocation { ServerName = GetServerName(server), DatabaseName = databaseName },
                    Type = "ServiceBrokerService",
                    Name = bs.Name,
                    Body = MapToString(bs.Script(_scriptingOption))
                };
                scriptCollection.Add(scriptInfo);
            }

            _logger?.LogInformation($"{method} on {server}({databaseName}) end.");
            return scriptCollection;
        }
        private List<MssqlScriptInfo> GetServiceBrokerRoutes(Server server, string databaseName)
        {
            const string method = nameof(GetServiceBrokerRoutes);
            _logger?.LogInformation($"{method} on {server}({databaseName}) begin.");

            var database = server.Databases[databaseName];
            var scriptCollection = new List<MssqlScriptInfo>(database.ServiceBroker.Routes.Count);

            foreach (ServiceRoute sr in database.ServiceBroker.Routes)
            {
                var scriptInfo = new MssqlScriptInfo
                {
                    Location = new ScriptInfoLocation { ServerName = GetServerName(server), DatabaseName = databaseName },
                    Type = "ServiceBrokerRoute",
                    Name = sr.Name,
                    Body = MapToString(sr.Script(_scriptingOption))
                };
                scriptCollection.Add(scriptInfo);
            }

            _logger?.LogInformation($"{method} on {server}({databaseName}) end.");
            return scriptCollection;
        }
        private List<MssqlScriptInfo> GetServiceBrokerRemoteBinding(Server server, string databaseName)
        {
            const string method = nameof(GetServiceBrokerRemoteBinding);
            _logger?.LogInformation($"{method} on {server}({databaseName}) begin.");

            var database = server.Databases[databaseName];
            var scriptCollection = new List<MssqlScriptInfo>(database.ServiceBroker.RemoteServiceBindings.Count);

            foreach(RemoteServiceBinding rsb in database.ServiceBroker.RemoteServiceBindings)
            {
                var scriptInfo = new MssqlScriptInfo
                {
                    Location = new ScriptInfoLocation { ServerName = GetServerName(server), DatabaseName = databaseName },
                    Type = "ServiceBrokerRemoteBinding",
                    Name = rsb.Name,
                    Body = MapToString(rsb.Script(_scriptingOption))
                };
                scriptCollection.Add(scriptInfo);
            }

            _logger?.LogInformation($"{method} on {server}({databaseName}) end.");
            return scriptCollection;
        }


        #endregion

        #region private static
        private static string MapToString(StringCollection collection)
        {
            if (collection == null || collection.Count <= 0)
                return null;

            var resultString = new StringBuilder();
            foreach (var str in collection)
            {
                resultString.AppendLine(str);
                resultString.AppendLine($"GO{Environment.NewLine}");
            }

            return resultString.ToString();
        }
        #endregion
    }
}
