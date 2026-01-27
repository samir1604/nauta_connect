using NautaCredential;
using NautaCredential.Contracts;
using NautaCredential.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace NautaConnect.Tests.NautaCredential.Tests;

[SupportedOSPlatform("windows")]
public class CredentialManagerTest : IDisposable
{
	private readonly CredentialManagerBase<UserCredentials> _sut;
	private readonly UserCredentials _testCredentials;

	public CredentialManagerTest()
	{
		_sut = new NautaCredentialManager();
		_testCredentials = new UserCredentials("testUser@nauta.com.cu", "P4ssw0rd_Secure_123!");
	}

	[Fact]
	public void SaveAndLoad_ShouldReturnIdenticalCredentials()
	{
		// ACT
		_sut.Save(_testCredentials);
		var loaded = _sut.Load();

		// ASSERT
		Assert.NotNull(loaded);
		Assert.Equal(_testCredentials.Username, loaded.Username);
		Assert.Equal(_testCredentials.Password, loaded.Password);
	}

	[Fact]
	public void Load_ShouldReturnNull_WhenFileDoesNotExist()
	{
		// ARRANGE
		_sut.Clear();

		// ACT
		var result = _sut.Load();

		// ASSERT
		Assert.Null(result);
	}

	[Fact]
	public void Clear_ShouldDeleteFileFromDisk()
	{
		// ARRANGE
		_sut.Save(_testCredentials);

		// ACT
		_sut.Clear();
		var result = _sut.Load();

		// ASSERT
		Assert.Null(result);
	}

	// Limpieza después de cada test para no dejar basura en tu AppData
	public void Dispose()
	{
		_sut.Clear();
	}
}
