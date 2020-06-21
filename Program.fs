open Farmer
open Farmer.Builders

let basename = "myapp"
let failoverLocation = Location.NorthEurope

let storage = storageAccount {
    name (basename + "storage")
}

let formRecogniser = cognitiveServices {
    name (basename + "formRecogniser")
    api CognitiveServices.FormRecognizer
    sku CognitiveServices.S1
}

let textAnalytics = cognitiveServices {
    name (basename + "textAnalytics")
    api CognitiveServices.TextAnalytics
    sku CognitiveServices.S1
}

let db = cosmosDb {
    account_name (basename + "cosmosaccount")
    throughput 400<CosmosDb.RU>

    name "azimageai"
    failover_policy (CosmosDb.AutoFailover failoverLocation)
    consistency_policy CosmosDb.Session

    add_containers [
        cosmosContainer {
            name "images"
            partition_key [ "/uid" ] CosmosDb.Hash
            add_unique_key [ "/uid" ]
        }
    ]
}

let simpleSettings = [
    "AZURE_STORAGE_BLOB_CONTAINER_NAME", "blobs"
    "AZURE_STORAGE_QUEUE_NAME",          "messages"
    "AZURE_STORAGE_QUEUE_MSG_COUNT",     "10"
    "AZURE_COSMOS_CONTAINER",            "images"
    "MEME_ENDPOINT",                     "https://meme-api.herokuapp.com/gimme/wholesomememes"
]

let apiName = basename + "api"

// Logs?
let app = webApp {
    name (basename + "app")
    sku WebApp.Sku.B1
    enable_managed_identity

    setting "AZURE_STORAGE_BLOB_ENDPOINT" storage.Key
    setting "AZURE_COSMOS_ENDPOINT"       db.Endpoint
    setting "AZURE_COSMOS_KEY"            db.PrimaryKey
    setting "AZURE_COSMOS_DB"             db.DbName
    setting "API_ENDPOINT"                (sprintf "http://%s.azurewebsites.net:2080" apiName)

    settings simpleSettings
    depends_on storage
    depends_on db

    //add_setting "AZURE_TEXT_ANALYTICS_ENDPOINT"     textAnalytics.??
    //add_setting "AZURE_FORM_RECOGNIZER_ENDPOINT"    formRecogniser.??

    // What's this?
    //"AZURE_STORAGE_QUEUE_ENDPOINT",      azurerm_storage_account.storage.primary_queue_endpoint
    // Not required
    // "APPINSIGHTS_INSTRUMENTATIONKEY",    azurerm_application_insights.logging.instrumentation_key

    // not supported yet...
    // logs {
    //     http_logs {
    //       file_system {
    //         retention_in_days = 1
    //         retention_in_mb   = 25
    //       }
    //     }
    //   }
}

let api = webApp {
    name apiName
    enable_managed_identity

    link_to_service_plan app.ServicePlan
    link_to_app_insights app.AppInsights

    setting "AZURE_STORAGE_BLOB_ENDPOINT" storage.Key
    setting "AZURE_COSMOS_ENDPOINT"       db.Endpoint
    setting "AZURE_COSMOS_KEY"            db.PrimaryKey
    setting "AZURE_COSMOS_DB"             db.DbName
    settings simpleSettings

    depends_on storage
    depends_on db
}

let template = arm {
    add_resources [
        storage
        formRecogniser
        textAnalytics
        db
        app
        api
    ]

    output "AZURE_COSMOS_ENDPOINT" db.Endpoint
    output "AZURE_COSMOS_KEY" db.PrimaryKey

    // Are these needed - you can just calculate them in F#?
    output "AZURE_FORM_RECOGNIZER_ENDPOINT" (sprintf "https://%s.cognitiveservices.azure.com/" formRecogniser.Name.Value)
    output "AZURE_TEXT_ANALYTICS_ENDPOINT" (sprintf "https://%s.cognitiveservices.azure.com/" textAnalytics.Name.Value)

    // Not supported yet?
    // output "AZURE_STORAGE_BLOB_ENDPOINT" azurerm_storage_account.storage.primary_blob_endpoint
    // output "AZURE_STORAGE_QUEUE_ENDPOINT" azurerm_storage_account.storage.primary_queue_endpoint

    // Needs to be surfaced from Farmer webapp
    //output "APPINSIGHTS_INSTRUMENTATIONKEY" app.AppInsightsKey
}