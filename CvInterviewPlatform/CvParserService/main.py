import os
import tempfile
import logging
from fastapi import FastAPI, UploadFile, File, HTTPException
from docling.datamodel.base_models import InputFormat
from docling.datamodel.pipeline_options import PdfPipelineOptions
from docling.document_converter import DocumentConverter, PdfFormatOption

# Configure logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger("cv-parser-service")

app = FastAPI(title="Docling CV Parser Service")

# Initialize Docling DocumentConverter at startup
logger.info("Initializing IBM Docling DocumentConverter...")
try:
    # Disable table structure parsing to skip downloading additional tableformer models (~40MB)
    pipeline_options = PdfPipelineOptions(do_table_structure=False)
    converter = DocumentConverter(
        format_options={
            InputFormat.PDF: PdfFormatOption(pipeline_options=pipeline_options)
        }
    )
    logger.info("IBM Docling DocumentConverter initialized successfully (table structure parsing disabled).")
except Exception as e:
    logger.error(f"Failed to initialize IBM Docling DocumentConverter: {e}")
    converter = None

@app.get("/health")
def health_check():
    return {"status": "ok", "converter_ready": converter is not None}

@app.post("/parse")
async def parse_pdf(file: UploadFile = File(...)):
    filename = (file.filename or "").strip('"').strip("'").lower()
    if not filename.endswith(".pdf"):
        logger.error(f"Validation failed: raw filename is '{file.filename}', processed filename is '{filename}'")
        raise HTTPException(
            status_code=400, 
            detail=f"Only PDF files are supported. Received: {file.filename}"
        )
        
    if converter is None:
        raise HTTPException(status_code=500, detail="IBM Docling converter is not initialized.")
        
    tmp_path = None
    try:
        # Create temp file
        with tempfile.NamedTemporaryFile(delete=False, suffix=".pdf") as tmp:
            content = await file.read()
            tmp.write(content)
            tmp_path = tmp.name
            
        logger.info(f"Received file: {file.filename}, saved to temporary path: {tmp_path}")
        
        # Convert using Docling
        result = converter.convert(tmp_path)
        markdown_text = result.document.export_to_markdown()
        
        logger.info(f"Successfully parsed {file.filename}")
        return {"markdown": markdown_text}
        
    except Exception as e:
        logger.error(f"Error parsing PDF: {e}")
        raise HTTPException(status_code=500, detail=f"Parsing failed: {str(e)}")
        
    finally:
        if tmp_path and os.path.exists(tmp_path):
            try:
                os.unlink(tmp_path)
            except Exception as ex:
                logger.error(f"Failed to delete temp file {tmp_path}: {ex}")

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="127.0.0.1", port=8000)
