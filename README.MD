# CollabVM Authentication Server
This is the authentication server for CollabVM. It is used alongside CollabVM server to add support for accounts.

## Prerequisites
- .NET Core 8.0 or later
- A MariaDB or MySQL server
- An SMTP server if you want email verification and password reset
- An hCaptcha account if you want to use hCaptcha to prevent bots

## Building the server
1. Clone the source code: `git clone https://git.computernewb.com/collabvm/CollabVMAuthServer --recursive`
2. Copy `config.example.toml` to `config.toml` and edit it to your liking
3. Install dependencies: `dotnet restore`
4. Build the server: `dotnet publish CollabVMAuthServer/CollabVMAuthServer.csproj -c Release --os linux -p:PublishReadyToRun=true`

## Run Database Migrations
You must do this the first time you run the server, and every time the database schema is updated (you will receive an error otherwise)
```
./CollabVMAuthServer/bin/Release/net8.0/linux-x64/publish/CollabVMAuthServer migrate-db
```

## Running the server
```
./CollabVMAuthServer/bin/Release/net8.0/linux-x64/publish/CollabVMAuthServer
```

## Setting up NGINX
You'll want to set up NGINX as a reverse proxy to the authentication server to add HTTPS support. Running the server over plain HTTP is strongly discouraged. Here is an example NGINX configuration:
```nginx
upstream cvm-auth { server 127.0.0.1:5858; } # Change the port if you changed the server port in config.toml
server {
    listen 80;
    server_name collabvm-auth.yourdomain.com;
    location / {
        # Uncomment if you use Cloudflare proxying
        #proxy_set_header X-Forwarded-For $http_cf_connecting_ip;
        # Comment if you use Cloudflare proxying
        proxy_set_header X-Forwarded-For $remote_addr; 
        proxy_pass http://cvm-auth;
    }
}
```
At this point, you should now enable SSL. To do this with LetsEncrypt, you can use `sudo certbot -i nginx -d collabvm-auth.yourdomain.com`

## Integrating with CollabVM
At this point, you can now integrate the authentication server with CollabVM. You can do this by editing the [auth] section of config.toml. Here is an example:
```toml
[auth]
enabled = true
apiEndpoint = "https://collabvm-auth.yourdomain.com"
secretKey = "hunter2" # Change this to the secret key you set in the authentication server's config.toml
```
You can then configure [auth.guestPermissions] to set the permissions for guests. Restart CollabVM after making these changes, and you should now have account support.