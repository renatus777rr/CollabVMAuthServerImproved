using System.CommandLine;
using System.CommandLine.Binding;
namespace Computernewb.CollabVMAuthServer;

public class AuthServerContext {
    public required IConfig Config { get; set; }
}

public class AuthServerCliOptionsBinder : BinderBase<AuthServerContext> {
    private readonly Option<string> _configPathOption;

    public AuthServerCliOptionsBinder(Option<string> configPathOption) {
        this._configPathOption = configPathOption;
    }

    protected override AuthServerContext GetBoundValue(BindingContext bindingContext)
    {
        var configPath = bindingContext.ParseResult.GetValueForOption(_configPathOption)!;
        // Load config file
        var config = IConfig.Load(configPath);

        return new AuthServerContext {
            Config = config,
        };
    }
}