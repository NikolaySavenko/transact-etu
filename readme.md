Проект для демонстрации методов оптимизации высоконагруженных приложений.  
В качестве высоконагруженного приложения будет система банковских транзакций.  
Первая часть проекта состоит из простого консольного приложения, которое будет генерировать тестовые транзакции,
которыми мы в последствии будем нагружать наше приложение.

цель проекта не сделать blazingly fast single instance, а добиться приближения к линейному росту производительности при горизонтальном масштабировании
можно было сделать проект на сях, который в single instance прорывал 300к rps, но при масшабировании до 10 инстансов деградировал или показывал ущербный рост.
