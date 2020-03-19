@echo the build is %1 
@echo Creating the RG
az group create --location westeurope --name azcopiertest%1
