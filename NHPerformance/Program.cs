using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NHibernate;
using NHibernate.Cfg;
using NHibernate.Dialect;
using NHibernate.Driver;
using NHibernate.Event;
using NHibernate.Linq;
using NHibernate.Mapping.ByCode;

namespace NHPerformance
{
	class Program
	{
		private const Int32 RepeatTimes = 20;
		private const Int32 NumberOfEntities = 100;

		static Configuration BuildConfiguration(RunMode mode)
		{
			switch (mode)
			{
				case RunMode.Normal:
					return (Normal());

				case RunMode.Optimized:
					return (Optimized());

				case RunMode.Stateless:
					return (Stateless());

				case RunMode.Sql:
					return (Sql());
			}

			return (null);
		}

		static Configuration Normal()
		{
			var cfg = Common();

			return (cfg);
		}

		static Configuration Common()
		{
			var cfg = new Configuration()
			.DataBaseIntegration(x =>
			{
				x.Dialect<MsSql2008Dialect>();
				x.Driver<Sql2008ClientDriver>();
				x.ConnectionStringName = "NHPerformance";
			});

			AddMapping(cfg, MappingMode.Conventional);

			return (cfg);
		}

		static Configuration Optimized()
		{
			var cfg = Common()
				.SetProperty(NHibernate.Cfg.Environment.FormatSql, Boolean.FalseString)
				.SetProperty(NHibernate.Cfg.Environment.GenerateStatistics, Boolean.FalseString)
				.SetProperty(NHibernate.Cfg.Environment.Hbm2ddlKeyWords, Hbm2DDLKeyWords.None.ToString())
				.SetProperty(NHibernate.Cfg.Environment.PrepareSql, Boolean.TrueString)
				.SetProperty(NHibernate.Cfg.Environment.PropertyBytecodeProvider, "lcg")
				.SetProperty(NHibernate.Cfg.Environment.PropertyUseReflectionOptimizer, Boolean.TrueString)
				.SetProperty(NHibernate.Cfg.Environment.QueryStartupChecking, Boolean.FalseString)
				.SetProperty(NHibernate.Cfg.Environment.ShowSql, Boolean.FalseString)
				.SetProperty(NHibernate.Cfg.Environment.StatementFetchSize, "100")
				.SetProperty(NHibernate.Cfg.Environment.UseProxyValidator, Boolean.FalseString)
				.SetProperty(NHibernate.Cfg.Environment.UseSecondLevelCache, Boolean.FalseString)
				.SetProperty(NHibernate.Cfg.Environment.UseSqlComments, Boolean.FalseString)
				.SetProperty(NHibernate.Cfg.Environment.UseQueryCache, Boolean.TrueString)
				.SetProperty(NHibernate.Cfg.Environment.WrapResultSets, Boolean.TrueString);

			cfg.EventListeners.PostLoadEventListeners = new IPostLoadEventListener[0];
			cfg.EventListeners.PreLoadEventListeners = new IPreLoadEventListener[0];

			return (cfg);
		}

		static Configuration Sql()
		{
			return (Optimized());
		}

		static Configuration Stateless()
		{
			return (Optimized());
		}

		static void Repeat(Action action, Int32 times)
		{
			for (var i = 0; i < times; ++i)
			{
				action();
			}
		}

		static Int64 Measure(Action action)
		{
			var watch = new Stopwatch();
			watch.Start();
			action();
			return (watch.ElapsedMilliseconds);
		}

		static Int64 ExecuteTest(Configuration cfg, RunMode mode)
		{
			using (var sessionFactory = cfg.BuildSessionFactory())
			{
				var milliseconds = Measure(() => Repeat(() => Run(sessionFactory, mode), RepeatTimes));
				Console.WriteLine("{0}: {1} milliseconds", mode, milliseconds);
				return (milliseconds);
			}
		}

		static void RunSql(ISessionFactory sessionFactory)
		{
			using (var session = sessionFactory.OpenStatelessSession())
			{
				//session.CreateSQLQuery("SELECT {v.*} FROM [Value] v").AddEntity("v", typeof(Value)).List<Value>();
				session.CreateSQLQuery("SELECT v.* FROM [Value] v").List();
			}
		}

		static void RunSession(ISessionFactory sessionFactory, RunMode mode)
		{
			using (var session = sessionFactory.OpenSession())
			{
				session.CacheMode = CacheMode.Ignore;
				session.FlushMode = FlushMode.Never;
				session.DefaultReadOnly = true;
				session.Query<Value>().ToList();
			}
		}

		static void RunStatelessSession(ISessionFactory sessionFactory)
		{
			using (var session = sessionFactory.OpenStatelessSession())
			{
				session.Query<Value>().ToList();
			}
		}

		static void Run(ISessionFactory sessionFactory, RunMode mode)
		{
			switch (mode)
			{
				case RunMode.Normal:
				case RunMode.Optimized:
					RunSession(sessionFactory, mode);
					break;

				case RunMode.Stateless:
					RunStatelessSession(sessionFactory);
					break;

				case RunMode.Sql:
					RunSql(sessionFactory);
					break;
			}
		}

		static void Add()
		{
			var cfg = Common()
				.SetProperty(NHibernate.Cfg.Environment.Hbm2ddlAuto, SchemaAutoAction.Create.ToString());

			AddMapping(cfg, MappingMode.Conventional);

			using (var sessionFactory = cfg.BuildSessionFactory())
			using (var session = sessionFactory.OpenSession())
			using (var tx = session.BeginTransaction())
			{
				var device = new Device { Name = "Device A" };
				var measure = new Measure { Name = "Measure A" };
				var now = DateTime.UtcNow;

				for (var i = 0; i < NumberOfEntities; ++i)
				{
					var value = new Value { Device = device, Measure = measure, Timestamp = now.AddSeconds(i), Val = i };
					session.Save(value);
				}

				session.Save(device);
				session.Save(measure);

				tx.Commit();
			}
		}
		
		static void AddStaticMapping(Configuration cfg)
		{
			var modelMapper = new ModelMapper();
			modelMapper.Class<Device>(x =>
			{
				x.Lazy(false);
				x.Id(y => y.DeviceId, y => y.Generator(Generators.Identity));
				x.Property(y => y.Name, y => y.NotNullable(true));
			});
			modelMapper.Class<Measure>(x =>
			{
				x.Lazy(false);
				x.Id(y => y.MeasureId, y => y.Generator(Generators.Identity));
				x.Property(y => y.Name, y => y.NotNullable(true));
			});
			modelMapper.Class<Value>(x =>
			{
				x.Lazy(false);
				x.Id(y => y.ValueId, y => y.Generator(Generators.Identity));
				x.Property(y => y.Val, y => y.NotNullable(true));
				x.Property(y => y.Timestamp, y => y.NotNullable(true));
				x.ManyToOne(y => y.Measure, y => y.NotNullable(true));
				x.ManyToOne(y => y.Device, y => y.NotNullable(true));
			});

			var mappings = modelMapper.CompileMappingForAllExplicitlyAddedEntities();

			cfg.AddMapping(mappings);
		}

		static void AddConventionalMapping(Configuration cfg)
		{
			var modelMapper = new ConventionModelMapper();
			modelMapper.IsEntity((x, y) => x.IsClass == true && x.IsSealed == false && x.Namespace == typeof(Program).Namespace);
			modelMapper.BeforeMapClass += (x, y, z) => { z.Id(a => a.Generator(Generators.Identity)); z.Lazy(false); };

			var mappings = modelMapper.CompileMappingFor(typeof(Program).Assembly.GetTypes().Where(x => x.IsPublic && x.IsSealed == false));

			cfg.AddMapping(mappings);
		}

		static void AddMapping(Configuration cfg, MappingMode mode)
		{
			switch (mode)
			{
				case MappingMode.Conventional:
					AddConventionalMapping(cfg);
					break;

				case MappingMode.Static:
					AddStaticMapping(cfg);
					break;
			}
		}

		static void Main(String[] args)
		{
			//Add();
			var configurations = new Dictionary<RunMode, Configuration>();
			var times = new Dictionary<RunMode, Int64>();

			foreach (var mode in Enum.GetValues(typeof(RunMode)).OfType<RunMode>())
			{
				configurations[mode] = BuildConfiguration(mode);
			}

			foreach (var mode in Enum.GetValues(typeof(RunMode)).OfType<RunMode>())
			{
				times[mode] = ExecuteTest(configurations[mode], mode);
			}
		}
	}
}
