@echo the build is %1 
@echo Deleting the RG
az group delete --name azcopiertest%1 --yes
