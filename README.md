# OPC UA to Azure Digital Twins

The OPC Unified Architecture (OPC UA) is a platform independent, service-oriented architecture for the manufacturing space. It is used to get telemetry data from devices.

Getting OPC UA Server data to flow into Azure Digital Twins requires multiple components installed on different devices, as well as some custom code and settings that need to be configured.

The files in this repo support [this article](https://docs.microsoft.com/azure/digital-twins/how-to-opcua-to-azure-digital-twins) shows that shows you how to connect your OPC UA nodes into Azure Digital Twins.

![opc ua to azure digital twins architecture diagram](../media/opcua-to-adt-diagram-1.png)

## Features

This project framework provides the following features:

* Simulation Example files for dtdl model, opcua-mapping.json, and opcua-mapping.json 
* Chocolate Factory Example files for opcua-mapping.json and opcua-mapping.json. See [readme.md](./Chocolate%20Factory%20Example) file for more information.
* Azure Function

## Getting Started

Follow the step-by-step guidance located in the [Azure Digital Twins article here](https://docs.microsoft.com/azure/digital-twins/how-to-opcua-to-azure-digital-twins)