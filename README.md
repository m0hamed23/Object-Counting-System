# Real-Time AI Object Counting and Monitoring System

[![Demo Video](https://img.youtube.com/vi/HO7m_LS0Clw/maxresdefault.jpg)](https://www.youtube.com/watch?v=HO7m_LS0Clw)

## Key Features

- **Real-Time Video Processing** - Supports multiple RTSP camera streams
- **AI-Powered Object Detection** - Utilizes ONNX models (YOLO or RF-DETR) for object detection 
- **Live Dashboard** - Displays real-time object counts by location or zone, alongside annotated video streams
- **Customizable Regions of Interest** - Easily define and manage detection zones within each camera feed
- **Automated Actions** - Configure actions to send the latest count data to external systems

## Technology Stack

- **Backend:** .NET 8, ASP.NET Core Web API
- **Frontend:** Angular 18
- **Database:** SQLite
- **AI Models:** RF-DETR Or YOLO
- **Video Processing:** FFmpeg

## Getting Started

This guide will walk you through setting up and running the application on Windows, Linux, or macOS.

### Prerequisites

Ensure you have the following software installed:

- **.NET 8 SDK** - [Download & Install](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Node.js 20.x (LTS)** - [Download & Install](https://nodejs.org/) (npm is included)
- **Git** - [Download & Install](https://git-scm.com/)

### Step 1: Clone the Repository

Open your terminal or command prompt and clone this repository to your local machine.

    git clone https://github.com/m0hamed23/Object-Counting-System.git
    cd Object-Counting-System
    
### Step 2: Download External Assets

The application relies on external binaries and models not included in this repository. Download and place them in the correct folders.

**Required directory structure:**

    Object-Counting-System/
    ├── CountingBackend/
    │   └── CountingWebAPI/
    │       ├── runtime_assets/
    │       │   ├── ffmpeg/
    │       │   │   └── bin/
    │       │   │       └── (FFmpeg library files: .dll on Windows, .so on Linux, .dylib on macOS)
    │       │   ├── Models/
    │       │   │   ├── yolov11s.onnx
    │       │   │   └── rf-detr-medium.onnx
    │       │   └── Counting.db (created automatically if it doesn't exist)
    │       ├── Controllers/
    │       └── (other backend files)
    └── CountingFrontend/
        └── (frontend files)
    
#### FFmpeg Binaries

**Purpose:** Decoding RTSP video streams

**Steps:**
1. Visit the [FFmpeg official builds page](https://ffmpeg.org/download.html) and download the build for your operating system
2. Extract the downloaded archive
3. Copy the library files from the lib folder to Backend/runtime_assets/ffmpeg/bin:
   - Windows: Copy .dll files
   - Linux: Copy .so files
   - macOS: Copy .dylib files

**Alternative - System-wide FFmpeg:**

The application is configured to first look in the runtime_assets folder. If it doesn't find FFmpeg there, it will search your system's PATH. If you prefer, you can install FFmpeg using your system's package manager and skip placing the binaries in the local folder:

    # Windows (using Chocolatey)
    choco install ffmpeg

    # Windows (using Winget)
    winget install FFmpeg

    # Debian/Ubuntu
    sudo apt-get install ffmpeg

    # macOS
    brew install ffmpeg
    
#### ONNX AI Models

**Purpose:** Object detection

**Steps:**
1. Download models from (https://drive.google.com/drive/folders/1EYOVHkvhGhUaJbQRK7w-_L3SJYAexY2s?usp=drive_link) 
2. Place files in runtime_assets/Models/:
   - yolov11s.onnx (or similar YOLO model)
   - rf-detr-medium.onnx (or another DETR model)
3. Ensure filenames match those in CountingBackend/CountingWebAPI/appsettings.json

### Step 3: Set up and Run the Backend

The backend server provides the API, serves the SignalR hub, and performs all video processing.

1. Open a terminal and navigate to the backend:

       cd CountingBackend/CountingWebAPI

2. Restore the .NET dependencies:

       dotnet restore

3. Run the application:

       dotnet run

The backend server will start and be accessible at http://localhost:5000. On the first run, it will automatically create the Counting.db SQLite database file in the runtime_assets folder.

### Step 4: Set up and Run the Frontend

The frontend is the Angular web application that you will interact with in your browser.

1. Open a separate terminal and navigate to frontend:

       cd CountingFrontend

2. Install the Node.js dependencies:

       npm install

3. Start the Angular development server:

       ng serve

The frontend development server will start and be accessible at http://localhost:4200.

### Step 5: Access the Application

1. Open your web browser and navigate to http://localhost:4200

2. You will be prompted to log in. Use the default credentials:
   - **Username:** admin
   - **Password:** admin

3. You can now start configuring cameras, zones, and locations through the UI!

**Note:** To find the RTSP URL format for your security cameras, refer to this [List of RTSP URLs by camera manufacturer](https://help.nsoft.vision/hc/en-us/articles/4411595651345-List-of-RTSP-URLs-of-security-camera-manufacturers) or search for it online.

## Hardware Acceleration for AI

The ONNX Runtime is configured to automatically detect and use NVIDIA CUDA for GPU acceleration when available, providing significantly faster object detection performance compared to CPU processing.

### CUDA Requirements

To enable GPU acceleration, you need:

- **NVIDIA GPU:** A CUDA-compatible NVIDIA graphics card
- **NVIDIA Drivers:** Latest drivers for your OS
- **CUDA Toolkit:** Version 12.x
- **cuDNN:** Version 9.x

**IMPORTANT:** This project uses ONNX Runtime v1.21.0. The CUDA Toolkit and cuDNN versions listed above are required for compatibility.

### Installation Guide

Follow the official ONNX Runtime CUDA setup guide:
- [ONNX Runtime CUDA ExecutionProvider Setup](https://onnxruntime.ai/docs/execution-providers/CUDA-ExecutionProvider.html)

This guide includes:
- CUDA and cuDNN version compatibility matrix
- Step-by-step installation instructions for Windows and Linux
- Verification steps

If no NVIDIA GPU is found or CUDA is not properly installed, the system will automatically fall back to using the CPU for processing.

## Credits

Demo video footage courtesy of [Pexels](https://www.pexels.com/):
- [Winter and snow in St. Petersburg, Russia](https://www.pexels.com/video/winter-and-snow-in-st-petersburg-russia-19892746/)
- [High angle view of parking lot](https://www.pexels.com/video/high-angle-view-of-parking-lot-3678248/)
- [A bus is driving down a highway with cars](https://www.pexels.com/video/a-bus-is-driving-down-a-highway-with-cars-26804235/)

