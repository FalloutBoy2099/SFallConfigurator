namespace SFallConfigurator.Configuration
{
	using System;

	/// <summary>
	/// Where an option reads its value from and writes it to.
	/// </summary>
	internal interface IConfigSource
	{
		/// <summary>
		/// Returns the raw ini value, or null when the key does not exist.
		/// </summary>
		String? Read(String? section, String key);

		/// <summary>
		/// Stores a raw ini value. Returns false when the key could not be written.
		/// </summary>
		Boolean Write(String? section, String key, String value);
	}

	/// <summary>
	/// A single ini document that may be replaced while the application runs.
	/// </summary>
	internal sealed class DocumentSource : IConfigSource
	{
		private readonly Func<IniDocument?> _document;
		private readonly IniWriteMode _writeMode;

		public DocumentSource(Func<IniDocument?> document, IniWriteMode writeMode)
		{
			_document = document;
			_writeMode = writeMode;
		}

		public String? Read(String? section, String key)
		{
			return _document()?.GetValue(section, key);
		}

		public Boolean Write(String? section, String key, String value)
		{
			return _document()?.SetValue(section, key, value, _writeMode) ?? false;
		}
	}

	/// <summary>
	/// The sfall options, spread over the optional custom sfall ini and ddraw.ini.
	/// Reads prefer the custom file; global writes go to whichever file holds the key.
	/// </summary>
	internal sealed class SfallSource : IConfigSource
	{
		private readonly GameConfiguration _configuration;

		public SfallSource(GameConfiguration configuration)
		{
			_configuration = configuration;
		}

		public String? Read(String? section, String key)
		{
			return _configuration.Sfall?.GetValue(section, key) ?? _configuration.Ddraw.GetValue(section, key);
		}

		public Boolean Write(String? section, String key, String value)
		{
			if (section is null)
			{
				return (_configuration.Sfall?.SetValue(null, key, value) ?? false)
					|| _configuration.Ddraw.SetValue(null, key, value);
			}

			IniDocument target = _configuration.Sfall is { LineCount: > 0 } custom ? custom : _configuration.Ddraw;
			return target.SetValue(section, key, value, IniWriteMode.AddIfMissing);
		}
	}
}