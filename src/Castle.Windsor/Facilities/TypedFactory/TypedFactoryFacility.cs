// Copyright 2004-2010 Castle Project - http://www.castleproject.org/
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace Castle.Facilities.TypedFactory
{
	using System;
	using System.ComponentModel;

	using Castle.Core;
	using Castle.Core.Configuration;
	using Castle.Facilities.TypedFactory.Internal;
	using Castle.MicroKernel;
	using Castle.MicroKernel.Facilities;
	using Castle.MicroKernel.Proxy;
	using Castle.MicroKernel.SubSystems.Conversion;
	using Castle.MicroKernel.Util;

	using Component = Castle.MicroKernel.Registration.Component;

	/// <summary>
	///   Summary description for TypedFactoryFacility.
	/// </summary>
	public class TypedFactoryFacility : AbstractFacility
	{
		public static readonly string DelegateFactoryKey = "Castle.TypedFactory.DelegateFactory";
		public static readonly string DelegateProxyFactoryKey = "Castle.TypedFactory.DelegateProxyFactory";
		public static readonly string InterceptorKey = "Castle.TypedFactory.Interceptor";

		[Obsolete("This method is obsolete. Use AsFactory() extension method on fluent registration API instead.")]
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public void AddTypedFactoryEntry(FactoryEntry entry)
		{
			var model = new ComponentModel(entry.Id, entry.FactoryInterface, typeof(Empty))
			{ LifestyleType = LifestyleType.Singleton };

			model.ExtendedProperties["typed.fac.entry"] = entry;
			model.Interceptors.Add(new InterceptorReference(typeof(FactoryInterceptor)));

			var proxyOptions = ProxyUtil.ObtainProxyOptions(model, true);
			proxyOptions.OmitTarget = true;

			((IKernelInternal)Kernel).AddCustomComponent(model);
		}

		protected virtual void AddFactories(IConfiguration facilityConfig, ITypeConverter converter)
		{
			if (facilityConfig == null)
			{
				return;
			}

			foreach (var config in facilityConfig.Children["factories"].Children)
			{
				var id = config.Attributes["id"];
				var creation = config.Attributes["creation"];
				var destruction = config.Attributes["destruction"];

				var factoryType = converter.PerformConversion<Type>(config.Attributes["interface"]);
				if (string.IsNullOrEmpty(creation))
				{
					var selector = config.Attributes["selector"];
					RegisterFactory(id, factoryType, selector);
					continue;
				}

				RegisterFactoryLegacy(creation, id, factoryType, destruction);
			}
		}

		protected override void Init()
		{
			InitFacility();

			LegacyInit();
		}

		private void InitFacility()
		{
			Kernel.Register(Component.For<TypedFactoryInterceptor>()
			                	.Named(InterceptorKey),
			                Component.For<DelegateFactory>()
			                	.Named(DelegateFactoryKey),
			                Component.For<IProxyFactoryExtension>()
			                	.ImplementedBy<DelegateProxyFactory>()
			                	.LifeStyle.Transient
			                	.Named(DelegateProxyFactoryKey));
		}

		private void LegacyInit()
		{
			Kernel.Register(Component.For<FactoryInterceptor>().Named("typed.fac.interceptor"));

			var converter = Kernel.GetConversionManager();
			AddFactories(FacilityConfig, converter);
		}

		private void RegisterFactory(string id, Type type, string selector)
		{
			var factory = Component.For(type).Named(id);
			if (selector == null)
			{
				factory.AsFactory();
			}
			else
			{
				var selectorKey = ReferenceExpressionUtil.ExtractComponentKey(selector);
				factory.AsFactory(x => x.SelectedWith(selectorKey));
			}

			Kernel.Register(factory);
		}

		private void RegisterFactoryLegacy(string creation, string id, Type factoryType, string destruction)
		{
			try
			{
#pragma warning disable 0618 //call to obsolete method
				AddTypedFactoryEntry(new FactoryEntry(id, factoryType, creation, destruction));
#pragma warning restore
			}
			catch (Exception)
			{
				var message = "Invalid factory entry in configuration";

				throw new Exception(message);
			}
		}
	}
}