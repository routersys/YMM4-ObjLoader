using Vortice.Direct3D11;
using Vortice.Direct3D;
using Vortice.DXGI;
using ObjLoader.Rendering.Managers.Interfaces;

namespace ObjLoader.Rendering.Managers
{
	internal sealed class ShadowMapManager : IShadowMapManager
	{
		public const int CascadeCount = 3;

		private ID3D11Texture2D? _shadowMapTexture;
		private ID3D11DepthStencilView[]? _shadowMapDSVs;
		private ID3D11ShaderResourceView? _shadowMapSRV;

		private readonly object _lock = new object();
		private int _currentShadowMapSize;
		private bool _isCascaded;
		private bool _disposed;

		public ID3D11Texture2D? ShadowMapTexture => _shadowMapTexture;
		public ID3D11DepthStencilView[]? ShadowMapDSVs => _shadowMapDSVs;
		public ID3D11ShaderResourceView? ShadowMapSRV => _shadowMapSRV;
		public int CurrentShadowMapSize => _currentShadowMapSize;
		public bool IsCascaded => _isCascaded;

		public void EnsureShadowMapSize(ID3D11Device device, int size, bool useCascaded)
		{
			if (device == null) return;

			lock (_lock)
			{
				if (_disposed) return;

				if (_currentShadowMapSize == size && _isCascaded == useCascaded && _shadowMapTexture != null)
				{
					return;
				}

				DisposeResources();

				_currentShadowMapSize = size;
				_isCascaded = useCascaded;

				int arraySize = useCascaded ? CascadeCount : 1;

				try
				{
					_shadowMapTexture = CreateShadowMapTexture(device, size, arraySize);
					_shadowMapDSVs = CreateDepthStencilViews(device, _shadowMapTexture, arraySize);
					_shadowMapSRV = CreateShaderResourceView(device, _shadowMapTexture, arraySize);
				}
				catch
				{
					DisposeResources();
					throw;
				}
			}
		}

		private static ID3D11Texture2D CreateShadowMapTexture(ID3D11Device device, int size, int arraySize)
		{
			var texDesc = new Texture2DDescription
			{
				Width = size,
				Height = size,
				MipLevels = 1,
				ArraySize = arraySize,
				Format = Format.R24G8_Typeless,
				SampleDescription = new SampleDescription(1, 0),
				Usage = ResourceUsage.Default,
				BindFlags = BindFlags.DepthStencil | BindFlags.ShaderResource,
				CPUAccessFlags = CpuAccessFlags.None,
				MiscFlags = ResourceOptionFlags.None
			};
			return device.CreateTexture2D(texDesc);
		}

		private static ID3D11DepthStencilView[] CreateDepthStencilViews(ID3D11Device device, ID3D11Texture2D texture, int arraySize)
		{
			var dsvs = new ID3D11DepthStencilView[arraySize];

			for (int i = 0; i < arraySize; i++)
			{
				var dsvDesc = new DepthStencilViewDescription
				{
					Format = Format.D24_UNorm_S8_UInt,
					ViewDimension = DepthStencilViewDimension.Texture2DArray,
					Texture2DArray = new Texture2DArrayDepthStencilView
					{
						ArraySize = 1,
						FirstArraySlice = i,
						MipSlice = 0
					}
				};
				dsvs[i] = device.CreateDepthStencilView(texture, dsvDesc);
			}

			return dsvs;
		}

		private static ID3D11ShaderResourceView CreateShaderResourceView(ID3D11Device device, ID3D11Texture2D texture, int arraySize)
		{
			var srvDesc = new ShaderResourceViewDescription
			{
				Format = Format.R24_UNorm_X8_Typeless,
				ViewDimension = ShaderResourceViewDimension.Texture2DArray,
				Texture2DArray = new Texture2DArrayShaderResourceView
				{
					ArraySize = arraySize,
					FirstArraySlice = 0,
					MipLevels = 1,
					MostDetailedMip = 0
				}
			};
			return device.CreateShaderResourceView(texture, srvDesc);
		}

		private void DisposeResources()
		{
			SafeDispose(ref _shadowMapSRV);

			if (_shadowMapDSVs != null)
			{
				foreach (var dsv in _shadowMapDSVs)
				{
					SafeDisposeValue(dsv);
				}
				_shadowMapDSVs = null;
			}

			SafeDispose(ref _shadowMapTexture);
		}

		private static void SafeDispose<T>(ref T? disposable) where T : class, IDisposable
		{
			var temp = disposable;
			disposable = null;
			SafeDisposeValue(temp);
		}

		private static void SafeDisposeValue(IDisposable? disposable)
		{
			if (disposable == null) return;
			try
			{
				disposable.Dispose();
			}
			catch
			{
			}
		}

		public void Dispose()
		{
			lock (_lock)
			{
				if (_disposed) return;
				_disposed = true;
				DisposeResources();
			}
		}
	}
}