import { Component, Input, Output, EventEmitter, AfterViewInit, ViewChild, ElementRef } from '@angular/core';
import { EditorView, basicSetup } from 'codemirror';
import { EditorState } from '@codemirror/state';
import { markdown as mdLang } from '@codemirror/lang-markdown';
import { oneDark } from '@codemirror/theme-one-dark';

@Component({
  selector: 'app-markdown-editor',
  standalone: true,
  template: `<div #editor class="border rounded overflow-hidden"></div>`
})
export class MarkdownEditorComponent implements AfterViewInit {
  @ViewChild('editor') editorRef!: ElementRef;
  @Input() value = '';
  @Output() valueChange = new EventEmitter<string>();
  private view?: EditorView;

  ngAfterViewInit() {
    const startState = EditorState.create({
      doc: this.value,
      extensions: [
        basicSetup,
        mdLang(),
        oneDark,
        EditorView.updateListener.of(update => {
          if (update.docChanged) {
            this.valueChange.emit(update.state.doc.toString());
          }
        })
      ]
    });
    this.view = new EditorView({ state: startState, parent: this.editorRef.nativeElement });
  }

  setValue(val: string) {
    if (this.view) {
      this.view.dispatch({
        changes: { from: 0, to: this.view.state.doc.length, insert: val }
      });
    }
  }
}
