@echo the build is %1 
@echo Creating the Redis cache
az group deployment create --template-file ./ARM_templates/redis_template.json --resource-group azcopiertest%1 --parameters redisCacheName=azcopierredis%1
